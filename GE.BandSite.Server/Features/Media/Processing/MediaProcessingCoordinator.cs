using System.Globalization;
using System.IO;
using System.Linq;
using GE.BandSite.Database;
using GE.BandSite.Database.Media;
using GE.BandSite.Server.Configuration;
using GE.BandSite.Server.Features.Media.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NodaTime;

namespace GE.BandSite.Server.Features.Media.Processing;

public interface IMediaProcessingCoordinator
{
    Task<int> ProcessPendingAsync(CancellationToken cancellationToken = default);
}

public sealed class MediaProcessingCoordinator : IMediaProcessingCoordinator
{
    private const int ProcessingErrorMaxLength = 400;

    private readonly IGeBandSiteDbContext _dbContext;
    private readonly IMediaTranscoder _transcoder;
    private readonly IImageOptimizer _imageOptimizer;
    private readonly IClock _clock;
    private readonly MediaProcessingOptions _options;
    private readonly MediaStorageOptions _storageOptions;
    private readonly IMediaStorageService _storageService;
    private readonly ILogger<MediaProcessingCoordinator> _logger;

    public MediaProcessingCoordinator(
        IGeBandSiteDbContext dbContext,
        IMediaTranscoder transcoder,
        IImageOptimizer imageOptimizer,
        IClock clock,
        IMediaStorageService storageService,
        IOptions<MediaProcessingOptions> options,
        IOptions<MediaStorageOptions> storageOptions,
        ILogger<MediaProcessingCoordinator> logger)
    {
        _dbContext = dbContext;
        _transcoder = transcoder;
        _imageOptimizer = imageOptimizer;
        _clock = clock;
        _storageService = storageService;
        _options = options.Value;
        _storageOptions = storageOptions.Value;
        _logger = logger;
    }

    public async Task<int> ProcessPendingAsync(CancellationToken cancellationToken = default)
    {
        var batchSize = Math.Max(1, _options.BatchSize);

        if (_options.PhotoOptimizationEnabled)
        {
            string? photoSourcePrefix = null;
            string? photoPlaybackPrefix = null;

            if (!string.IsNullOrWhiteSpace(_storageOptions.PhotoSourcePrefix))
            {
                photoSourcePrefix = _storageService.NormalizeKey(_storageOptions.PhotoSourcePrefix);
            }

            if (!string.IsNullOrWhiteSpace(_storageOptions.PhotoPrefix))
            {
                photoPlaybackPrefix = _storageService.NormalizeKey(_storageOptions.PhotoPrefix);
            }

            if (!string.IsNullOrWhiteSpace(photoSourcePrefix))
            {
                var readyPhotos = await _dbContext.MediaAssets
                    .Where(x => x.AssetType == MediaAssetType.Photo && x.ProcessingState == MediaProcessingState.Ready)
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);

                var updated = 0;

                foreach (var photo in readyPhotos)
                {
                    var storagePath = photo.SourcePath ?? photo.StoragePath;
                    if (string.IsNullOrWhiteSpace(storagePath))
                    {
                        continue;
                    }

                    if (!storagePath.StartsWith(photoSourcePrefix, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    if (!string.IsNullOrWhiteSpace(photo.PlaybackPath) && photoPlaybackPrefix != null && photo.PlaybackPath.StartsWith(photoPlaybackPrefix, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    photo.PlaybackPath = EnsurePhotoPlaybackPath(photo);
                    photo.ProcessingState = MediaProcessingState.Pending;
                    photo.ProcessingError = null;
                    updated++;
                }

                if (updated > 0)
                {
                    await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                }
            }
        }

        var pending = await _dbContext.MediaAssets
            .Where(x => x.ProcessingState == MediaProcessingState.Pending
                && (x.AssetType == MediaAssetType.Video || (_options.PhotoOptimizationEnabled && x.AssetType == MediaAssetType.Photo)))
            .OrderBy(x => x.CreatedAt)
            .Take(batchSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (pending.Count == 0)
        {
            return 0;
        }

        foreach (var asset in pending)
        {
            asset.ProcessingState = MediaProcessingState.Processing;
            asset.ProcessingError = null;
        }

        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var processed = 0;

        foreach (var asset in pending)
        {
            try
            {
                await ProcessAssetAsync(asset, cancellationToken).ConfigureAwait(false);
                processed++;
                _logger.LogInformation("Media asset {AssetId} ({AssetType}) processed successfully.", asset.Id, asset.AssetType);
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Failed to process media asset {AssetId} ({AssetType})", asset.Id, asset.AssetType);
                asset.ProcessingState = MediaProcessingState.Error;
                asset.ProcessingError = TruncateProcessingError(exception.Message);
                asset.LastProcessedAt = _clock.GetCurrentInstant();
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return processed;
    }

    private async Task ProcessAssetAsync(Database.Media.MediaAsset asset, CancellationToken cancellationToken)
    {
        switch (asset.AssetType)
        {
            case MediaAssetType.Video:
                await ProcessVideoAssetAsync(asset, cancellationToken).ConfigureAwait(false);
                break;
            case MediaAssetType.Photo when _options.PhotoOptimizationEnabled:
                await ProcessPhotoAssetAsync(asset, cancellationToken).ConfigureAwait(false);
                break;
            case MediaAssetType.Photo:
                throw new InvalidOperationException("Photo optimization is disabled but a photo asset was queued for processing.");
            default:
                throw new NotSupportedException($"Unsupported media asset type {asset.AssetType}.");
        }
    }

    private async Task ProcessVideoAssetAsync(Database.Media.MediaAsset asset, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(asset.SourcePath) && string.IsNullOrWhiteSpace(asset.StoragePath))
        {
            throw new InvalidOperationException($"Asset {asset.Id} does not define a source path.");
        }

        if (string.IsNullOrWhiteSpace(asset.SourcePath) && !string.IsNullOrWhiteSpace(asset.StoragePath))
        {
            asset.SourcePath = asset.StoragePath;
        }

        var sourceKey = asset.SourcePath ?? asset.StoragePath;
        var inputPath = await _storageService.EnsureLocalCopyAsync(sourceKey!, cancellationToken).ConfigureAwait(false);
        var playbackRelativePath = EnsureVideoPlaybackPath(asset);

        var outputPath = CreateTranscodeOutputPath(asset.Id);

        var result = await _transcoder.TranscodeAsync(new MediaTranscodeRequest(inputPath, outputPath), cancellationToken).ConfigureAwait(false);

        await _storageService.UploadFromFileAsync(playbackRelativePath, outputPath, "video/mp4", cancellationToken).ConfigureAwait(false);

        TryDelete(outputPath);

        asset.PlaybackPath = _storageService.NormalizeKey(playbackRelativePath);
        asset.DurationSeconds = result.DurationSeconds ?? asset.DurationSeconds;
        asset.Width = result.Width ?? asset.Width;
        asset.Height = result.Height ?? asset.Height;
        asset.LastProcessedAt = _clock.GetCurrentInstant();
        asset.ProcessingState = MediaProcessingState.Ready;
        asset.ProcessingError = null;

        if (string.IsNullOrWhiteSpace(asset.StoragePath))
        {
            asset.StoragePath = asset.PlaybackPath;
        }

        _logger.LogDebug("Video asset {AssetId} metadata updated: duration {Duration}s, resolution {Width}x{Height}.", asset.Id, asset.DurationSeconds, asset.Width, asset.Height);
    }

    private async Task ProcessPhotoAssetAsync(Database.Media.MediaAsset asset, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(asset.SourcePath) && string.IsNullOrWhiteSpace(asset.StoragePath))
        {
            throw new InvalidOperationException($"Asset {asset.Id} does not define a source path.");
        }

        if (string.IsNullOrWhiteSpace(asset.SourcePath) && !string.IsNullOrWhiteSpace(asset.StoragePath))
        {
            asset.SourcePath = asset.StoragePath;
        }

        var sourceKey = asset.SourcePath ?? asset.StoragePath;
        var inputPath = await _storageService.EnsureLocalCopyAsync(sourceKey!, cancellationToken).ConfigureAwait(false);
        var playbackRelativePath = EnsurePhotoPlaybackPath(asset);

        var outputPath = CreateImageOutputPath(asset.Id);

        var optimizationOptions = new ImageOptimizationOptions(
            _options.PhotoMaxWidth,
            _options.PhotoMaxHeight,
            _options.PhotoJpegQuality);

        var result = await _imageOptimizer.OptimizeAsync(inputPath, outputPath, optimizationOptions, cancellationToken).ConfigureAwait(false);

        await _storageService.UploadFromFileAsync(playbackRelativePath, outputPath, "image/jpeg", cancellationToken).ConfigureAwait(false);

        TryDelete(outputPath);

        asset.PlaybackPath = _storageService.NormalizeKey(playbackRelativePath);
        asset.Width = result.Width;
        asset.Height = result.Height;
        asset.DurationSeconds = null;
        asset.LastProcessedAt = _clock.GetCurrentInstant();
        asset.ProcessingState = MediaProcessingState.Ready;
        asset.ProcessingError = null;

        _logger.LogDebug("Photo asset {AssetId} optimized to {Width}x{Height}.", asset.Id, asset.Width, asset.Height);
    }

    private string EnsurePhotoPlaybackPath(Database.Media.MediaAsset asset)
    {
        if (!string.IsNullOrWhiteSpace(asset.PlaybackPath))
        {
            var normalizedExisting = _storageService.NormalizeKey(asset.PlaybackPath);
            if (normalizedExisting.EndsWith("_web.jpg", StringComparison.OrdinalIgnoreCase))
            {
                return normalizedExisting;
            }
        }

        if (string.IsNullOrWhiteSpace(_storageOptions.PhotoPrefix))
        {
            throw new InvalidOperationException("MediaStorage:PhotoPrefix must be configured to optimize photos.");
        }

        var prefix = _storageService.NormalizeKey(_storageOptions.PhotoPrefix);
        var baseName = BuildSanitizedBaseName(asset.SourcePath ?? asset.StoragePath ?? asset.PlaybackPath ?? asset.Id.ToString("N", CultureInfo.InvariantCulture));
        var fileName = string.Concat(baseName, "_web.jpg");
        var relative = string.IsNullOrWhiteSpace(prefix) ? fileName : string.Join('/', prefix, fileName);
        var path = _storageService.NormalizeKey(relative);
        asset.PlaybackPath = path;
        return path;
    }

    private string EnsureVideoPlaybackPath(Database.Media.MediaAsset asset)
    {
        if (!string.IsNullOrWhiteSpace(asset.PlaybackPath))
        {
            var normalizedExisting = _storageService.NormalizeKey(asset.PlaybackPath);
            if (normalizedExisting.EndsWith("_mp4.mp4", StringComparison.OrdinalIgnoreCase))
            {
                return normalizedExisting;
            }
        }

        var prefix = _storageService.NormalizeKey(_storageOptions.VideoPlaybackPrefix);
        var baseName = BuildSanitizedBaseName(asset.SourcePath ?? asset.StoragePath ?? asset.PlaybackPath ?? asset.Id.ToString("N", CultureInfo.InvariantCulture));
        var fileName = string.Concat(baseName, "_mp4.mp4");
        var relative = string.IsNullOrWhiteSpace(prefix) ? fileName : string.Join('/', prefix, fileName);
        var path = _storageService.NormalizeKey(relative);
        asset.PlaybackPath = path;
        return path;
    }

    private string CreateTranscodeOutputPath(Guid assetId)
    {
        var baseDirectory = _options.TempDirectory;
        if (string.IsNullOrWhiteSpace(baseDirectory))
        {
            baseDirectory = Path.Combine(Path.GetTempPath(), "ge-band-site", "transcode");
        }

        Directory.CreateDirectory(baseDirectory);
        return Path.Combine(baseDirectory, $"{assetId:N}-{Guid.NewGuid():N}.mp4");
    }

    private string CreateImageOutputPath(Guid assetId)
    {
        var baseDirectory = _options.TempDirectory;
        if (string.IsNullOrWhiteSpace(baseDirectory))
        {
            baseDirectory = Path.Combine(Path.GetTempPath(), "ge-band-site", "optimize");
        }

        Directory.CreateDirectory(baseDirectory);
        return Path.Combine(baseDirectory, $"{assetId:N}-{Guid.NewGuid():N}.jpg");
    }

    private static string TruncateProcessingError(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return "Transcoding failed.";
        }

        if (message.Length <= ProcessingErrorMaxLength)
        {
            return message;
        }

        var trimmed = message.AsSpan(0, ProcessingErrorMaxLength - 3).TrimEnd();
        return string.Concat(trimmed.ToString(), "...");
    }

    private void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException exception)
        {
            _logger.LogWarning(exception, "Failed to delete temporary media output {File}", path);
        }
    }

    private static string BuildSanitizedBaseName(string? path)
    {
        var fileName = Path.GetFileNameWithoutExtension(path ?? string.Empty);
        if (string.IsNullOrWhiteSpace(fileName))
        {
            fileName = "asset";
        }

        var sanitized = new string(fileName
            .Select(ch => char.IsLetterOrDigit(ch) ? ch : '-')
            .ToArray());

        while (sanitized.Contains("--", StringComparison.Ordinal))
        {
            sanitized = sanitized.Replace("--", "-", StringComparison.Ordinal);
        }

        sanitized = sanitized.Trim('-');
        return string.IsNullOrWhiteSpace(sanitized) ? "asset" : sanitized;
    }
}
