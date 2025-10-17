using GE.BandSite.Server.Features.Operations.Deliverability;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GE.BandSite.Server.Pages.Admin.Deliverability;

[Authorize]
public class IndexModel : PageModel
{
    private readonly IDeliverabilityReportService _reportService;

    public IndexModel(IDeliverabilityReportService reportService)
    {
        _reportService = reportService;
    }

    public DeliverabilityDashboardModel Dashboard { get; private set; } = new(
        Array.Empty<DeliverabilitySuppressionModel>(),
        Array.Empty<DeliverabilityEventModel>());

    [TempData]
    public string? StatusMessage { get; set; }

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        Dashboard = await _reportService.GetDashboardAsync(100, 50, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IActionResult> OnPostReleaseAsync(Guid suppressionId, string? reason, CancellationToken cancellationToken)
    {
        var released = await _reportService.ReleaseSuppressionAsync(suppressionId, reason, cancellationToken).ConfigureAwait(false);
        StatusMessage = released ? "Suppression released." : "Suppression could not be released.";
        return RedirectToPage();
    }
}
