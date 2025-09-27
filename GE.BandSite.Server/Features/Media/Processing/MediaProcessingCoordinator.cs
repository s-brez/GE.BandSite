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
    private readonly IGeBandSiteDbContext _dbContext;
    private readonly IMediaTranscoder _transcoder;
    private readonly IClock _clock;
    private readonly MediaProcessingOptions _options;
    private readonly MediaStorageOptions _storageOptions;
    private readonly IMediaStorageService _storageService;
    private readonly ILogger<MediaProcessingCoordinator> _logger;

    public MediaProcessingCoordinator(
        IGeBandSiteDbContext dbContext,
        IMediaTranscoder transcoder,
        IClock clock,
        IMediaStorageService storageService,
        IOptions<MediaProcessingOptions> options,
        IOptions<MediaStorageOptions> storageOptions,
        ILogger<MediaProcessingCoordinator> logger)
    {
        _dbContext = dbContext;
        _transcoder = transcoder;
        _clock = clock;
        _storageService = storageService;
        _options = options.Value;
        _storageOptions = storageOptions.Value;
        _logger = logger;
    }

    public async Task<int> ProcessPendingAsync(CancellationToken cancellationToken = default)
    {
        var batchSize = Math.Max(1, _options.BatchSize);

        var pending = await _dbContext.MediaAssets
            .Where(x => x.AssetType == MediaAssetType.Video && x.ProcessingState == MediaProcessingState.Pending)
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
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Failed to transcode media asset {AssetId}", asset.Id);
                asset.ProcessingState = MediaProcessingState.Error;
                asset.ProcessingError = exception.Message;
                asset.LastProcessedAt = _clock.GetCurrentInstant();
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return processed;
    }

    private async Task ProcessAssetAsync(Database.Media.MediaAsset asset, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(asset.SourcePath) && string.IsNullOrWhiteSpace(asset.StoragePath))
        {
            throw new InvalidOperationException($"Asset {asset.Id} does not define a source path.");
        }

        var sourceKey = asset.SourcePath ?? asset.StoragePath;
        var inputPath = await _storageService.EnsureLocalCopyAsync(sourceKey!, cancellationToken).ConfigureAwait(false);
        var playbackRelativePath = DeterminePlaybackPath(asset);

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

        // Ensure storage path points to playback asset for public serving when no dedicated value exists.
        if (string.IsNullOrWhiteSpace(asset.StoragePath))
        {
            asset.StoragePath = asset.PlaybackPath;
        }
    }

    private string DeterminePlaybackPath(Database.Media.MediaAsset asset)
    {
        if (!string.IsNullOrWhiteSpace(asset.PlaybackPath))
        {
            return _storageService.NormalizeKey(asset.PlaybackPath);
        }

        var prefix = _storageService.NormalizeKey(_storageOptions.VideoPlaybackPrefix);
        var fileName = string.Concat(asset.Id.ToString("N", System.Globalization.CultureInfo.InvariantCulture), ".mp4");
        return _storageService.NormalizeKey(string.Join('/', prefix, fileName));
    }

    private string CreateTranscodeOutputPath(Guid assetId)
    {
        var baseDirectory = _options.TempDirectory;
        if (string.IsNullOrWhiteSpace(baseDirectory))
        {
            baseDirectory = Path.Combine(Path.GetTempPath(), "ge-band-media");
        }

        Directory.CreateDirectory(baseDirectory);
        return Path.Combine(baseDirectory, $"{assetId:N}-{Guid.NewGuid():N}.mp4");
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
}
