using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using GE.BandSite.Server.Features.Contact;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.RazorPages;
using NodaTime;
using NodaTime.Text;
using NodaTime.TimeZones;
using Newtonsoft.Json;

namespace GE.BandSite.Server.Pages.Contact;

/// <summary>
/// Provides the booking enquiry touch points and placeholder form.
/// </summary>
public class IndexModel : PageModel
{
    private readonly IContactSubmissionService _submissionService;
    private const string SubmissionSuccessMessage = "Thank you! Our bookings team will reply within one business day.";

    public IndexModel(IContactSubmissionService submissionService)
    {
        _submissionService = submissionService;
    }

    public string HeroTitle { get; private set; } = string.Empty;

    public string HeroLead { get; private set; } = string.Empty;

    public ContactManager Manager { get; private set; } = ContactManager.Empty;

    public ContactFormDefinition FormDefinition { get; private set; } = ContactFormDefinition.Empty;

    public IReadOnlyList<FaqItem> FaqEntries { get; private set; } = Array.Empty<FaqItem>();

    public string ResponseCommitment { get; private set; } = string.Empty;

    [BindProperty]
    public InputModel Input { get; set; } = new();

    [TempData]
    public string? SubmissionMessage { get; set; }

    public bool SubmissionSucceeded => !string.IsNullOrEmpty(SubmissionMessage);

    public void OnGet()
    {
        PopulateStaticContent();
        Input = new InputModel();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        PopulateStaticContent();
        ValidateEventTiming();

        if (IsJsonRequest())
        {
            return await HandleJsonSubmissionAsync(cancellationToken).ConfigureAwait(false);
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var request = BuildSubmissionRequest();
        var result = await _submissionService.SubmitAsync(request, cancellationToken).ConfigureAwait(false);
        if (!result.Success)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error);
            }

            return Page();
        }

        SubmissionMessage = SubmissionSuccessMessage;
        return RedirectToPage();
    }

    private void PopulateStaticContent()
    {
        HeroTitle = "Ready to Book?";
        HeroLead = "Bring Swing The Boogie to your next event!";

        Manager = new ContactManager("Gilbert Ernest",
            "Band Manager",
            "bookings@swingtheboogie.com",
            "+61 402 148 140",
            "+61 402 148 140");

        FormDefinition = new ContactFormDefinition(
            new List<SelectOption>
            {
                new("Corporate Event", "Corporate Event"),
                new("Wedding", "Wedding"),
                new("Private Function", "Private Function"),
                new("Festival", "Festival"),
            },
            new List<SelectOption>
            {
                new("Solo", "Solo"),
                new("Duo", "Duo"),
                new("5-Piece", "5-Piece"),
                new("10-Piece", "10-Piece"),
            },
            BuildTimeZoneOptions());

        FaqEntries = new List<FaqItem>
        {
            new("Do you travel internationally?", "Yes, worldwide."),
            new("Can you supply AV and staging?", "Yes, packages can include full AV setup."),
            new("Can we choose the band size?", "Absolutely – from solo to full 10-piece."),
        };

        ResponseCommitment = "We aim to respond to all enquiries within 3 business days.";
    }

    private async Task<IActionResult> HandleJsonSubmissionAsync(CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            var response = ContactSubmissionResponse.FromModelState(ModelState, "Please review the highlighted fields.");
            return JsonContent(response, StatusCodes.Status400BadRequest);
        }

        var request = BuildSubmissionRequest();
        var result = await _submissionService.SubmitAsync(request, cancellationToken).ConfigureAwait(false);
        if (!result.Success)
        {
            var response = ContactSubmissionResponse.FromSubmissionErrors("We could not submit your booking request.", result.Errors);
            return JsonContent(response, StatusCodes.Status400BadRequest);
        }

        var successResponse = ContactSubmissionResponse.FromSuccess(SubmissionSuccessMessage);
        return JsonContent(successResponse, StatusCodes.Status200OK);
    }

    private bool IsJsonRequest()
    {
        if (Request.Headers.TryGetValue("Accept", out var acceptValues))
        {
            if (acceptValues.Any(value => value != null &&
                value.IndexOf("application/json", StringComparison.OrdinalIgnoreCase) >= 0))
            {
                return true;
            }
        }

        if (Request.Headers.TryGetValue("X-Requested-With", out var requestedWith))
        {
            if (requestedWith.Any(value => value != null &&
                (string.Equals(value, "XMLHttpRequest", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(value, "fetch", StringComparison.OrdinalIgnoreCase))))
            {
                return true;
            }
        }

        return false;
    }

    private void ValidateEventTiming()
    {
        var hasDateTime = Input.EventDateTime.HasValue;
        var timeZoneId = Input.EventTimezone?.Trim();
        var hasTimeZone = !string.IsNullOrWhiteSpace(timeZoneId);

        if (!hasDateTime && !hasTimeZone)
        {
            return;
        }

        if (hasDateTime && !hasTimeZone)
        {
            ModelState.AddModelError($"{nameof(Input)}.{nameof(Input.EventTimezone)}", "Select the timezone for your event.");
            return;
        }

        if (!hasDateTime && hasTimeZone)
        {
            ModelState.AddModelError($"{nameof(Input)}.{nameof(Input.EventTimezone)}", "Add the event date and time that matches the selected timezone.");
            return;
        }

        if (hasTimeZone && DateTimeZoneProviders.Tzdb.GetZoneOrNull(timeZoneId!) is null)
        {
            ModelState.AddModelError($"{nameof(Input)}.{nameof(Input.EventTimezone)}", "Select a valid timezone.");
            return;
        }

        Input.EventTimezone = timeZoneId;
    }

    private ContactSubmissionRequest BuildSubmissionRequest()
    {
        return new ContactSubmissionRequest(
            Input.OrganizerName!,
            Input.OrganizerEmail!,
            Input.OrganizerPhone,
            Input.EventType!,
            ConvertToLocalDate(Input.EventDateTime, Input.EventTimezone),
            Input.EventTimezone,
            Input.Location,
            Input.PreferredBandSize!,
            ContactSubmissionDefaults.BudgetRangePlaceholder,
            Input.Message);
    }

    private static ContentResult JsonContent(ContactSubmissionResponse payload, int statusCode)
    {
        var serialized = JsonConvert.SerializeObject(payload);
        return new ContentResult
        {
            Content = serialized,
            ContentType = "application/json",
            StatusCode = statusCode,
        };
    }

    private static LocalDate? ConvertToLocalDate(DateTime? eventDateTime, string? timeZoneId)
    {
        if (!eventDateTime.HasValue)
        {
            return null;
        }

        var localDateTime = LocalDateTime.FromDateTime(DateTime.SpecifyKind(eventDateTime.Value, DateTimeKind.Unspecified));
        var normalizedTimeZoneId = string.IsNullOrWhiteSpace(timeZoneId) ? null : timeZoneId.Trim();

        if (!string.IsNullOrWhiteSpace(normalizedTimeZoneId))
        {
            var provider = DateTimeZoneProviders.Tzdb;
            var zone = provider.GetZoneOrNull(normalizedTimeZoneId);
            if (zone is not null)
            {
                var zoned = zone.AtLeniently(localDateTime);
                return zoned.Date;
            }
        }

        return localDateTime.Date;
    }

    private static IReadOnlyList<SelectOption> BuildTimeZoneOptions()
    {
        var provider = DateTimeZoneProviders.Tzdb;
        var offsetPattern = OffsetPattern.CreateWithInvariantCulture("+HH:mm");
        var instant = SystemClock.Instance.GetCurrentInstant();
        var zoneIds = new[]
        {
            "Etc/UTC",
            "Europe/London",
            "Europe/Paris",
            "Europe/Berlin",
            "America/New_York",
            "America/Chicago",
            "America/Denver",
            "America/Los_Angeles",
            "America/Sao_Paulo",
            "Asia/Singapore",
            "Asia/Dubai",
            "Asia/Tokyo",
            "Australia/Sydney",
            "Australia/Melbourne",
            "Pacific/Auckland"
        };

        var options = new List<SelectOption>();
        foreach (var zoneId in zoneIds)
        {
            var zone = provider.GetZoneOrNull(zoneId);
            if (zone is null)
            {
                continue;
            }

            var offset = zone.GetUtcOffset(instant);
            var offsetLabel = offsetPattern.Format(offset);
            var friendlyName = zoneId switch
            {
                "Etc/UTC" => "UTC",
                _ => zoneId.Replace('_', ' ')
            };

            options.Add(new SelectOption(zoneId, $"{friendlyName} (UTC{offsetLabel})"));
        }

        return options;
    }

    public sealed record ContactManager(string Name, string Title, string Email, string PhoneDisplay, string PhoneDial)
    {
        public static ContactManager Empty { get; } = new(string.Empty, string.Empty, string.Empty, string.Empty, string.Empty);
    }

    public sealed record ContactFormDefinition(
        IReadOnlyList<SelectOption> EventTypes,
        IReadOnlyList<SelectOption> BandSizes,
        IReadOnlyList<SelectOption> TimeZones)
    {
        public static ContactFormDefinition Empty { get; } = new(
            Array.Empty<SelectOption>(),
            Array.Empty<SelectOption>(),
            Array.Empty<SelectOption>());
    }

    public sealed record SelectOption(string Value, string Label);

    public sealed record FaqItem(string Question, string Answer);

    public sealed class InputModel
    {
        [Required]
        [Display(Name = "Your name")]
        public string? OrganizerName { get; set; }

        [Required]
        [EmailAddress]
        [Display(Name = "Email address")]
        public string? OrganizerEmail { get; set; }

        [Phone]
        [Display(Name = "Phone number")]
        public string? OrganizerPhone { get; set; }

        [Required]
        [Display(Name = "Event type")]
        public string? EventType { get; set; }

        [Display(Name = "Event date & time")]
        public DateTime? EventDateTime { get; set; }

        [Display(Name = "Event timezone")]
        public string? EventTimezone { get; set; }

        [Display(Name = "Location")]
        [StringLength(200)]
        public string? Location { get; set; }

        [Required]
        [Display(Name = "Preferred band size")]
        public string? PreferredBandSize { get; set; }

        [Display(Name = "Tell us about your vision")]
        [StringLength(2000)]
        public string? Message { get; set; }
    }

    [JsonObject(MemberSerialization.OptIn)]
    public sealed class ContactSubmissionResponse
    {
        private static readonly IReadOnlyDictionary<string, string[]> EmptyFieldErrors =
            new Dictionary<string, string[]>(StringComparer.Ordinal);

        private static readonly IReadOnlyList<string> EmptyGeneralErrors =
            Array.Empty<string>();

        private ContactSubmissionResponse(bool success, string? message, IReadOnlyDictionary<string, string[]> fieldErrors, IReadOnlyList<string> errors)
        {
            Success = success;
            Message = message;
            FieldErrors = fieldErrors;
            Errors = errors;
        }

        [JsonProperty("success")]
        public bool Success { get; }

        [JsonProperty("message")]
        public string? Message { get; }

        [JsonProperty("field_errors")]
        public IReadOnlyDictionary<string, string[]> FieldErrors { get; }

        [JsonProperty("errors")]
        public IReadOnlyList<string> Errors { get; }

        public static ContactSubmissionResponse FromSuccess(string message)
        {
            return new ContactSubmissionResponse(true, message, EmptyFieldErrors, EmptyGeneralErrors);
        }

        public static ContactSubmissionResponse FromSubmissionErrors(string? message, IReadOnlyList<string> errors)
        {
            var parsedErrors = errors?
                .Where(error => !string.IsNullOrWhiteSpace(error))
                .Select(error => error.Trim())
                .ToArray() ?? Array.Empty<string>();

            return new ContactSubmissionResponse(false, message, EmptyFieldErrors, parsedErrors);
        }

        public static ContactSubmissionResponse FromModelState(ModelStateDictionary modelState, string? message)
        {
            var fieldErrors = modelState
                .Where(entry => !string.IsNullOrEmpty(entry.Key))
                .Select(entry => new
                {
                    entry.Key,
                    Errors = (entry.Value?.Errors ?? new ModelErrorCollection())
                        .Select(error => error.ErrorMessage)
                        .Where(error => !string.IsNullOrWhiteSpace(error))
                        .Select(error => error.Trim())
                        .ToArray(),
                })
                .Where(entry => entry.Errors.Length > 0)
                .ToDictionary(entry => entry.Key, entry => entry.Errors, StringComparer.Ordinal);

            var generalErrors = modelState
                .Where(entry => string.IsNullOrEmpty(entry.Key))
                .SelectMany(entry => (entry.Value?.Errors ?? new ModelErrorCollection()))
                .Select(error => error.ErrorMessage)
                .Where(error => !string.IsNullOrWhiteSpace(error))
                .Select(error => error.Trim())
                .ToArray();

            return new ContactSubmissionResponse(false, message, fieldErrors, generalErrors);
        }
    }
}
