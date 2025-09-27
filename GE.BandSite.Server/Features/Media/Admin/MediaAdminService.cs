using GE.BandSite.Database;
using GE.BandSite.Database.Media;
using GE.BandSite.Server.Configuration;
using GE.BandSite.Server.Features.Media.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NodaTime;

namespace GE.BandSite.Server.Features.Media.Admin;

public sealed class MediaAdminService : IMediaAdminService
{
    private readonly IGeBandSiteDbContext _dbContext;
    private readonly IMediaStorageService _storageService;
    private readonly IClock _clock;
    private readonly MediaStorageOptions _storageOptions;
    private readonly ILogger<MediaAdminService> _logger;

    public MediaAdminService(
        IGeBandSiteDbContext dbContext,
        IMediaStorageService storageService,
        IClock clock,
        IOptions<MediaStorageOptions> storageOptions,
        ILogger<MediaAdminService> logger)
    {
        _dbContext = dbContext;
        _storageService = storageService;
        _clock = clock;
        _storageOptions = storageOptions.Value;
        _logger = logger;
    }

    public Task<PresignedUploadResponse> CreateUploadAsync(MediaUploadRequest request, CancellationToken cancellationToken = default)
    {
        return _storageService.CreateUploadAsync(request, cancellationToken);
    }

    public async Task<MediaAsset> CreatePhotoAssetAsync(CreatePhotoAssetParameters parameters, CancellationToken cancellationToken = default)
    {
        ValidatePhotoParameters(parameters);

        var assetId = Guid.NewGuid();
        var storagePath = await _storageService.PromotePhotoAsync(parameters.RawObjectKey, assetId, parameters.Title, parameters.ContentType, cancellationToken).ConfigureAwait(false);

        var asset = new MediaAsset
        {
            Id = assetId,
            Title = parameters.Title,
            Description = parameters.Description,
            AssetType = MediaAssetType.Photo,
            StoragePath = storagePath,
            SourcePath = storagePath,
            PlaybackPath = null,
            PosterPath = null,
            ProcessingState = MediaProcessingState.Ready,
            IsFeatured = parameters.IsFeatured,
            ShowOnHome = parameters.ShowOnHome,
            IsPublished = parameters.IsPublished,
            DisplayOrder = parameters.DisplayOrder,
            CreatedAt = _clock.GetCurrentInstant()
        };

        await PersistAsync(asset, cancellationToken).ConfigureAwait(false);
        return asset;
    }

    public async Task<MediaAsset> CreateVideoAssetAsync(CreateVideoAssetParameters parameters, CancellationToken cancellationToken = default)
    {
        ValidateVideoParameters(parameters);

        var assetId = Guid.NewGuid();
        var storagePath = await _storageService.PromoteVideoSourceAsync(parameters.RawVideoKey, assetId, parameters.Title, parameters.VideoContentType, cancellationToken).ConfigureAwait(false);

        string? posterPath = null;
        if (!string.IsNullOrWhiteSpace(parameters.RawPosterKey) && !string.IsNullOrWhiteSpace(parameters.PosterContentType))
        {
            try
            {
                posterPath = await _storageService.PromotePosterAsync(parameters.RawPosterKey, assetId, parameters.Title + " poster", parameters.PosterContentType!, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                _logger.LogWarning(exception, "Failed to promote poster for video asset {AssetId}. Continuing without poster.", assetId);
            }
        }

        var playbackRelative = BuildPlaybackPath(assetId);

        var asset = new MediaAsset
        {
            Id = assetId,
            Title = parameters.Title,
            Description = parameters.Description,
            AssetType = MediaAssetType.Video,
            StoragePath = storagePath,
            SourcePath = storagePath,
            PlaybackPath = playbackRelative,
            PosterPath = posterPath,
            ProcessingState = MediaProcessingState.Pending,
            IsFeatured = parameters.IsFeatured,
            ShowOnHome = parameters.ShowOnHome,
            IsPublished = parameters.IsPublished,
            DisplayOrder = parameters.DisplayOrder,
            CreatedAt = _clock.GetCurrentInstant()
        };

        await PersistAsync(asset, cancellationToken).ConfigureAwait(false);
        return asset;
    }

    private async Task PersistAsync(MediaAsset asset, CancellationToken cancellationToken)
    {
        await _dbContext.MediaAssets.AddAsync(asset, cancellationToken).ConfigureAwait(false);
        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private string BuildPlaybackPath(Guid assetId)
    {
        var basePrefix = _storageService.NormalizeKey(_storageOptions.VideoPlaybackPrefix);
        var fileName = string.Concat(assetId.ToString("N", System.Globalization.CultureInfo.InvariantCulture), ".mp4");
        return _storageService.NormalizeKey(string.Join('/', basePrefix, fileName));
    }

    private static void ValidatePhotoParameters(CreatePhotoAssetParameters parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        ArgumentException.ThrowIfNullOrWhiteSpace(parameters.Title);
        ArgumentException.ThrowIfNullOrWhiteSpace(parameters.RawObjectKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(parameters.ContentType);
    }

    private static void ValidateVideoParameters(CreateVideoAssetParameters parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        ArgumentException.ThrowIfNullOrWhiteSpace(parameters.Title);
        ArgumentException.ThrowIfNullOrWhiteSpace(parameters.RawVideoKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(parameters.VideoContentType);
    }
}
