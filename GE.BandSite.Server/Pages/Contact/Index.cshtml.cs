using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using GE.BandSite.Server.Features.Contact;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using NodaTime;

namespace GE.BandSite.Server.Pages.Contact;

/// <summary>
/// Provides the booking enquiry touch points and placeholder form.
/// </summary>
public class IndexModel : PageModel
{
    private readonly IContactSubmissionService _submissionService;

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

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var request = new ContactSubmissionRequest(
            Input.OrganizerName!,
            Input.OrganizerEmail!,
            Input.OrganizerPhone,
            Input.EventType!,
            ConvertToLocalDate(Input.EventDate),
            Input.Location,
            Input.PreferredBandSize!,
            Input.BudgetRange!,
            Input.Message);

        var result = await _submissionService.SubmitAsync(request, cancellationToken).ConfigureAwait(false);
        if (!result.Success)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error);
            }

            return Page();
        }

        SubmissionMessage = "Thank you! Our bookings team will reply within one business day.";
        return RedirectToPage();
    }

    private void PopulateStaticContent()
    {
        HeroTitle = "Let’s shape an unforgettable event.";
        HeroLead = "Share a few details and the Swing The Boogie team will craft a line-up, repertoire, and run sheet tailored to your celebration.";

        Manager = new ContactManager("Vivian Brooks",
            "Touring & Bookings Manager",
            "bookings@swingtheboogie.com",
            "+1 (312) 555-0191",
            "+13125550191");

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
                new("Solo / Duo", "Solo / Duo"),
                new("5-Piece", "5-Piece"),
                new("7-Piece", "7-Piece"),
                new("10-Piece", "10-Piece"),
            },
            new List<SelectOption>
            {
                new("Under 10k", "Under $10k"),
                new("10k-20k", "$10k – $20k"),
                new("20k-40k", "$20k – $40k"),
                new("40k+", "$40k+"),
            });

        FaqEntries = new List<FaqItem>
        {
            new("Do you travel internationally?", "Yes. Our team is passport-ready and manages carnets, backline specs, and freight logistics globally."),
            new("Can you supply AV and staging?", "We coordinate with venue or third-party AV partners, and can provide a preferred vendor list when needed."),
            new("How flexible is the repertoire?", "We build set lists around your vision—mixing classic swing, modern pop-swing remixes, and custom arrangements on request."),
        };

        ResponseCommitment = "Expect a tailored reply within one business day.";
    }

    private static LocalDate? ConvertToLocalDate(DateOnly? date) => date.HasValue
        ? LocalDate.FromDateTime(date.Value.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified))
        : null;

    public sealed record ContactManager(string Name, string Title, string Email, string PhoneDisplay, string PhoneDial)
    {
        public static ContactManager Empty { get; } = new(string.Empty, string.Empty, string.Empty, string.Empty, string.Empty);
    }

    public sealed record ContactFormDefinition(
        IReadOnlyList<SelectOption> EventTypes,
        IReadOnlyList<SelectOption> BandSizes,
        IReadOnlyList<SelectOption> BudgetRanges)
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

        [Display(Name = "Event date")]
        public DateOnly? EventDate { get; set; }

        [Display(Name = "Location")]
        [StringLength(200)]
        public string? Location { get; set; }

        [Required]
        [Display(Name = "Preferred band size")]
        public string? PreferredBandSize { get; set; }

        [Required]
        [Display(Name = "Budget range")]
        public string? BudgetRange { get; set; }

        [Display(Name = "Tell us about your vision")]
        [StringLength(2000)]
        public string? Message { get; set; }
    }
}
