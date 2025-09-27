using GE.BandSite.Database;
using GE.BandSite.Database.Media;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace GE.BandSite.Server.Pages.Admin.Media;

[Authorize]
public class IndexModel : PageModel
{
    private readonly IGeBandSiteDbContext _dbContext;

    public IndexModel(IGeBandSiteDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public IReadOnlyList<MediaAsset> MediaAssets { get; private set; } = Array.Empty<MediaAsset>();

    public async Task OnGetAsync()
    {
        MediaAssets = await _dbContext.MediaAssets
            .OrderBy(x => x.DisplayOrder)
            .ThenBy(x => x.CreatedAt)
            .ToListAsync()
            .ConfigureAwait(false);
    }

    public async Task<IActionResult> OnPostUpdateAsync(Guid id, bool isPublished, bool showOnHome)
    {
        var asset = await _dbContext.MediaAssets.FirstOrDefaultAsync(x => x.Id == id).ConfigureAwait(false);
        if (asset != null)
        {
            asset.IsPublished = isPublished;
            asset.ShowOnHome = showOnHome;
            await _dbContext.SaveChangesAsync().ConfigureAwait(false);
        }

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostReprocessAsync(Guid id)
    {
        var asset = await _dbContext.MediaAssets.FirstOrDefaultAsync(x => x.Id == id).ConfigureAwait(false);
        if (asset != null)
        {
            asset.ProcessingState = MediaProcessingState.Pending;
            asset.ProcessingError = null;
            await _dbContext.SaveChangesAsync().ConfigureAwait(false);
        }

        return RedirectToPage();
    }
}
