using GE.BandSite.Server.Features.Contact;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GE.BandSite.Server.Pages.Admin.ContactSubmissions;

[Authorize]
public class IndexModel : PageModel
{
    private readonly IContactSubmissionService _submissionService;

    public IndexModel(IContactSubmissionService submissionService)
    {
        _submissionService = submissionService;
    }

    public IReadOnlyList<ContactSubmissionListItem> Submissions { get; private set; } = Array.Empty<ContactSubmissionListItem>();

    [TempData]
    public string? StatusMessage { get; set; }

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        Submissions = await _submissionService.GetRecentAsync(100, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IActionResult> OnPostDeleteAsync(Guid submissionId, CancellationToken cancellationToken)
    {
        var deleted = await _submissionService.DeleteAsync(submissionId, cancellationToken).ConfigureAwait(false);
        StatusMessage = deleted ? "Submission removed." : "Submission already removed.";
        return RedirectToPage();
    }
}
