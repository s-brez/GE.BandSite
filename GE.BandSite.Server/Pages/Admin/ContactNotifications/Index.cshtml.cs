using System.ComponentModel.DataAnnotations;
using GE.BandSite.Server.Features.Contact;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GE.BandSite.Server.Pages.Admin.ContactNotifications;

[Authorize]
public class IndexModel : PageModel
{
    private static readonly char[] Separators = { '\r', '\n', ',', ';' };

    private readonly IContactNotificationSettingsService _settingsService;

    public IndexModel(IContactNotificationSettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    public IReadOnlyList<string> Recipients { get; private set; } = Array.Empty<string>();

    [BindProperty]
    [Display(Name = "Notification recipients")]
    public string RecipientEntries { get; set; } = string.Empty;

    [TempData]
    public string? StatusMessage { get; set; }

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        Recipients = await _settingsService.GetRecipientEmailsAsync(cancellationToken).ConfigureAwait(false);
        RecipientEntries = string.Join(Environment.NewLine, Recipients);
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        var parseResult = ParseEntries(RecipientEntries);
        if (parseResult.Errors.Count > 0)
        {
            foreach (var error in parseResult.Errors)
            {
                ModelState.AddModelError(nameof(RecipientEntries), error);
            }

            Recipients = await _settingsService.GetRecipientEmailsAsync(cancellationToken).ConfigureAwait(false);
            return Page();
        }

        await _settingsService.UpdateRecipientsAsync(parseResult.Emails, cancellationToken).ConfigureAwait(false);

        StatusMessage = parseResult.Emails.Count == 0
            ? "Recipients cleared. Notifications remain disabled until at least one address is saved."
            : $"Recipients updated ({parseResult.Emails.Count}).";

        return RedirectToPage();
    }

    private static ParseResult ParseEntries(string entries)
    {
        if (string.IsNullOrWhiteSpace(entries))
        {
            return new ParseResult(Array.Empty<string>(), Array.Empty<string>());
        }

        var validator = new EmailAddressAttribute();
        var emails = new List<string>();
        var errors = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var token in entries.Split(Separators, StringSplitOptions.RemoveEmptyEntries))
        {
            var candidate = token.Trim();
            if (!validator.IsValid(candidate))
            {
                errors.Add($"'{candidate}' is not a valid email address.");
                continue;
            }

            if (seen.Add(candidate))
            {
                emails.Add(candidate);
            }
        }

        return new ParseResult(emails, errors);
    }

    private sealed record ParseResult(IReadOnlyList<string> Emails, IReadOnlyList<string> Errors);
}
