using GE.BandSite.Database;
using GE.BandSite.Database.Media;
using GE.BandSite.Server.Configuration;
using GE.BandSite.Server.Features.Media.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace GE.BandSite.Server.Features.Media;

public sealed class MediaQueryService : IMediaQueryService
{
    private readonly IGeBandSiteDbContext _dbContext;
    private readonly MediaDeliveryOptions _options;

    public MediaQueryService(IGeBandSiteDbContext dbContext, IOptions<MediaDeliveryOptions> options)
    {
        _dbContext = dbContext;
        _options = options.Value;
    }

    public async Task<HomeMediaModel> GetHomeHighlightsAsync(CancellationToken cancellationToken = default)
    {
        var assets = await _dbContext.MediaAssets
            .Where(x => x.IsPublished && x.ShowOnHome)
            .OrderBy(x => x.DisplayOrder)
            .Select(x => new MediaAssetProjection(
                x.Id,
                x.Title,
                x.Description,
                x.StoragePath,
                x.PlaybackPath,
                x.PosterPath,
                x.AssetType,
                x.MediaAssetTags.Select(t => t.MediaTag.Name),
                x.ProcessingState))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var featuredVideo = assets
            .Where(x => x.AssetType == MediaAssetType.Video && x.ProcessingState == MediaProcessingState.Ready)
            .Select(Map)
            .FirstOrDefault();

        var highlightPhotos = assets
            .Where(x => x.AssetType == MediaAssetType.Photo && x.ProcessingState == MediaProcessingState.Ready)
            .Select(Map)
            .ToList();

        return new HomeMediaModel(featuredVideo, highlightPhotos);
    }

    public async Task<MediaGalleryModel> GetGalleryAsync(CancellationToken cancellationToken = default)
    {
        var assets = await _dbContext.MediaAssets
            .Where(x => x.IsPublished)
            .OrderBy(x => x.DisplayOrder)
            .Select(x => new MediaAssetProjection(
                x.Id,
                x.Title,
                x.Description,
                x.StoragePath,
                x.PlaybackPath,
                x.PosterPath,
                x.AssetType,
                x.MediaAssetTags.Select(t => t.MediaTag.Name),
                x.ProcessingState))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var videos = assets
            .Where(x => x.AssetType == MediaAssetType.Video && x.ProcessingState == MediaProcessingState.Ready)
            .Select(Map)
            .ToList();

        var photos = assets
            .Where(x => x.AssetType == MediaAssetType.Photo && x.ProcessingState == MediaProcessingState.Ready)
            .Select(Map)
            .ToList();

        return new MediaGalleryModel(videos, photos);
    }

    private MediaItem Map(MediaAssetProjection asset)
    {
        var hasPlayback = !string.IsNullOrWhiteSpace(asset.PlaybackPath);
        string assetPath = hasPlayback ? asset.PlaybackPath! : asset.StoragePath;

        string url = BuildUrl(assetPath);
        string? posterUrl = !string.IsNullOrWhiteSpace(asset.PosterPath) ? BuildUrl(asset.PosterPath) : null;
        var tags = asset.Tags.ToList();

        return new MediaItem(
            asset.Id,
            asset.Title,
            asset.Description,
            url,
            posterUrl,
            tags,
            asset.AssetType.ToString());
    }

    private string BuildUrl(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        if (Uri.TryCreate(path, UriKind.Absolute, out var absolute))
        {
            return absolute.ToString();
        }

        if (!string.IsNullOrWhiteSpace(_options.BaseUrl))
        {
            return string.Concat(_options.BaseUrl.TrimEnd('/'), "/", path.TrimStart('/'));
        }

        return "/" + path.TrimStart('/');
    }
}

internal sealed record MediaAssetProjection(
    Guid Id,
    string Title,
    string? Description,
    string StoragePath,
    string? PlaybackPath,
    string? PosterPath,
    MediaAssetType AssetType,
    IEnumerable<string> Tags,
    MediaProcessingState ProcessingState);
