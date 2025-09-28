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

    public IReadOnlyList<AdminMediaAssetViewModel> MediaAssets { get; private set; } = Array.Empty<AdminMediaAssetViewModel>();

    public async Task OnGetAsync()
    {
        MediaAssets = await _dbContext.MediaAssets
            .OrderBy(x => x.DisplayOrder)
            .ThenBy(x => x.CreatedAt)
            .Select(x => new AdminMediaAssetViewModel(
                x.Id,
                x.Title,
                x.Description,
                x.AssetType,
                x.IsPublished,
                x.ShowOnHome,
                x.IsFeatured,
                x.DisplayOrder,
                x.ProcessingState,
                x.ProcessingError,
                x.PlaybackPath,
                x.StoragePath,
                x.PosterPath))
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

public sealed class AdminMediaAssetViewModel
{
    public AdminMediaAssetViewModel(
        Guid id,
        string title,
        string? description,
        MediaAssetType assetType,
        bool isPublished,
        bool showOnHome,
        bool isFeatured,
        int displayOrder,
        MediaProcessingState processingState,
        string? processingError,
        string? playbackPath,
        string? storagePath,
        string? posterPath)
    {
        Id = id;
        Title = title;
        Description = description;
        AssetType = assetType;
        IsPublished = isPublished;
        ShowOnHome = showOnHome;
        IsFeatured = isFeatured;
        DisplayOrder = displayOrder;
        ProcessingState = processingState;
        ProcessingError = processingError;
        PlaybackPath = playbackPath;
        StoragePath = storagePath;
        PosterPath = posterPath;
    }

    public Guid Id { get; }
    public string Title { get; }
    public string? Description { get; }
    public MediaAssetType AssetType { get; }
    public bool IsPublished { get; }
    public bool ShowOnHome { get; }
    public bool IsFeatured { get; }
    public int DisplayOrder { get; }
    public MediaProcessingState ProcessingState { get; }
    public string? ProcessingError { get; }
    public string? PlaybackPath { get; }
    public string? StoragePath { get; }
    public string? PosterPath { get; }

    public string? DeliveryPath => ProcessingState == MediaProcessingState.Ready && !string.IsNullOrWhiteSpace(PlaybackPath)
        ? PlaybackPath
        : null;

    public bool HasDeliveryPath => !string.IsNullOrWhiteSpace(DeliveryPath);
}
