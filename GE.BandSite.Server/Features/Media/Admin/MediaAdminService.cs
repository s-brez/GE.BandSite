using System;
using System.Globalization;
using System.IO;
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
        var originalFileName = ExtractFileName(parameters.RawObjectKey, parameters.Title, GuessExtension(parameters.ContentType, ".jpg"));
        var storagePath = await _storageService.PromotePhotoAsync(parameters.RawObjectKey, assetId, originalFileName, parameters.ContentType, cancellationToken).ConfigureAwait(false);

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

    public async Task<MediaAsset> CreateVideoAssetAsync(CreateVideoAssetParameters parameters, CancellationToken cancellationToken = default)
    {
        ValidateVideoParameters(parameters);

        var assetId = Guid.NewGuid();
        var originalFileName = ExtractFileName(parameters.RawVideoKey, parameters.Title, GuessExtension(parameters.VideoContentType, ".mp4"));
        var storagePath = await _storageService.PromoteVideoSourceAsync(parameters.RawVideoKey, assetId, originalFileName, parameters.VideoContentType, cancellationToken).ConfigureAwait(false);

        string? posterPath = null;
        if (!string.IsNullOrWhiteSpace(parameters.RawPosterKey) && !string.IsNullOrWhiteSpace(parameters.PosterContentType))
        {
            try
            {
                var posterFileName = ExtractFileName(parameters.RawPosterKey, parameters.Title + " poster", GuessExtension(parameters.PosterContentType!, ".jpg"));
                posterPath = await _storageService.PromotePosterAsync(parameters.RawPosterKey, assetId, posterFileName, parameters.PosterContentType!, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                _logger.LogWarning(exception, "Failed to promote poster for video asset {AssetId}. Continuing without poster.", assetId);
            }
        }

        var asset = new MediaAsset
        {
            Id = assetId,
            Title = parameters.Title,
            Description = parameters.Description,
            AssetType = MediaAssetType.Video,
            StoragePath = storagePath,
            SourcePath = storagePath,
            PlaybackPath = null,
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

    private static string ExtractFileName(string? rawKey, string fallback, string defaultExtension)
    {
        if (!string.IsNullOrWhiteSpace(rawKey))
        {
            var fileName = Path.GetFileName(rawKey);
            if (!string.IsNullOrWhiteSpace(fileName))
            {
                return fileName;
            }
        }

        var safeFallback = fallback.Replace(' ', '-');
        if (!defaultExtension.StartsWith(".", StringComparison.Ordinal))
        {
            defaultExtension = "." + defaultExtension;
        }

        return string.Concat(safeFallback, defaultExtension);
    }

    private static string GuessExtension(string contentType, string fallback)
    {
        return contentType switch
        {
            "image/jpeg" => ".jpg",
            "image/png" => ".png",
            "image/webp" => ".webp",
            "video/quicktime" => ".mov",
            "video/mp4" => ".mp4",
            _ => fallback
        };
    }
}
