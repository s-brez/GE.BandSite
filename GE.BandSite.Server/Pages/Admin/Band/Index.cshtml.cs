using GE.BandSite.Database.Organization;
using GE.BandSite.Server.Features.Organization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GE.BandSite.Server.Pages.Admin.Band;

[Authorize]
public class IndexModel : PageModel
{
    private readonly IOrganizationAdminService _adminService;

    public IndexModel(IOrganizationAdminService adminService)
    {
        _adminService = adminService;
    }

    public IReadOnlyList<BandMemberProfile> BandMembers { get; private set; } = Array.Empty<BandMemberProfile>();

    [BindProperty]
    public BandMemberProfile Input { get; set; } = new();

    public async Task OnGetAsync()
    {
        BandMembers = await _adminService.GetBandAsync().ConfigureAwait(false);
    }

    public async Task<IActionResult> OnPostSaveAsync(CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            BandMembers = await _adminService.GetBandAsync(cancellationToken).ConfigureAwait(false);
            return Page();
        }

        await _adminService.AddOrUpdateBandMemberAsync(Input, cancellationToken).ConfigureAwait(false);
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        await _adminService.DeleteBandMemberAsync(id, cancellationToken).ConfigureAwait(false);
        return RedirectToPage();
    }

    public async Task<IActionResult> OnGetEditAsync(Guid id, CancellationToken cancellationToken)
    {
        BandMembers = await _adminService.GetBandAsync(cancellationToken).ConfigureAwait(false);
        var member = BandMembers.FirstOrDefault(x => x.Id == id);
        if (member != null)
        {
            Input = new BandMemberProfile
            {
                Id = member.Id,
                Name = member.Name,
                Role = member.Role,
                Spotlight = member.Spotlight,
                DisplayOrder = member.DisplayOrder,
                IsActive = member.IsActive,
                CreatedAt = member.CreatedAt
            };
        }

        return Page();
    }
}
