using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Amazon.S3;
using Amazon.S3.Model;
using GE.BandSite.Database;
using GE.BandSite.Database.Media;
using GE.BandSite.Server.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NodaTime;

namespace GE.BandSite.Server.Features.Media;

public sealed class MediaStorageBootstrapper
{
    private readonly IGeBandSiteDbContext _dbContext;
    private readonly IAmazonS3 _s3Client;
    private readonly MediaStorageOptions _storageOptions;
    private readonly IClock _clock;
    private readonly ILogger<MediaStorageBootstrapper> _logger;

    public MediaStorageBootstrapper(
        IGeBandSiteDbContext dbContext,
        IAmazonS3 s3Client,
        IOptions<MediaStorageOptions> storageOptions,
        IClock clock,
        ILogger<MediaStorageBootstrapper> logger)
    {
        _dbContext = dbContext;
        _s3Client = s3Client;
        _clock = clock;
        _logger = logger;
        _storageOptions = storageOptions.Value;
    }

    public async Task EnsureAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_storageOptions.BucketName))
        {
            _logger.LogInformation("Media storage bucket not configured; skipping S3 media import.");
            return;
        }

        try
        {
            await CleanupDuplicateDeliveryAssetsAsync(cancellationToken).ConfigureAwait(false);

            var existingKeys = await _dbContext.MediaAssets
                .Select(x => x.StoragePath)
                .ToHashSetAsync(StringComparer.OrdinalIgnoreCase, cancellationToken)
                .ConfigureAwait(false);

            var displayOrder = await _dbContext.MediaAssets
                .Select(x => (int?)x.DisplayOrder)
                .MaxAsync(cancellationToken)
                .ConfigureAwait(false) ?? 0;

            var posterLookup = await BuildLookupAsync(_storageOptions.PosterPrefix, cancellationToken).ConfigureAwait(false);
            var playbackLookup = await BuildLookupAsync(_storageOptions.VideoPlaybackPrefix, cancellationToken).ConfigureAwait(false);
            var photoOptimizedLookup = string.IsNullOrWhiteSpace(_storageOptions.PhotoPrefix)
                ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                : await BuildLookupAsync(_storageOptions.PhotoPrefix, cancellationToken).ConfigureAwait(false);

            var assets = new List<MediaAsset>();

            // Import video sources
            await foreach (var key in ListKeysAsync(_storageOptions.VideoSourcePrefix, cancellationToken).ConfigureAwait(false))
            {
                if (existingKeys.Contains(key))
                {
                    continue;
                }

                var relative = GetRelativeKey(key, _storageOptions.VideoSourcePrefix);
                var baseKey = RemoveExtension(relative);
                playbackLookup.TryGetValue(baseKey, out var playbackKey);
                posterLookup.TryGetValue(baseKey, out var posterKey);

                displayOrder++;
                assets.Add(CreateVideoAsset(key, playbackKey, posterKey, playbackKey != null ? MediaProcessingState.Ready : MediaProcessingState.Pending, displayOrder));

                if (!string.IsNullOrWhiteSpace(playbackKey))
                {
                    existingKeys.Add(playbackKey);
                }
            }

            // Import playback-only videos (e.g. already-processed MP4s)
            await foreach (var key in ListKeysAsync(_storageOptions.VideoPlaybackPrefix, cancellationToken).ConfigureAwait(false))
            {
                if (existingKeys.Contains(key))
                {
                    continue;
                }

                var relative = GetRelativeKey(key, _storageOptions.VideoPlaybackPrefix);
                var baseKey = RemoveExtension(relative);
                posterLookup.TryGetValue(baseKey, out var posterKey);

                displayOrder++;
                assets.Add(CreateVideoAsset(key, key, posterKey, MediaProcessingState.Ready, displayOrder));
            }

            if (!string.IsNullOrWhiteSpace(_storageOptions.PhotoSourcePrefix))
            {
                await foreach (var key in ListKeysAsync(_storageOptions.PhotoSourcePrefix, cancellationToken).ConfigureAwait(false))
                {
                    if (existingKeys.Contains(key))
                    {
                        continue;
                    }

                    var relative = GetRelativeKey(key, _storageOptions.PhotoSourcePrefix);
                    var baseKey = RemoveExtension(relative);
                    string? optimizedKey = null;

                    if (!photoOptimizedLookup.TryGetValue(baseKey, out var match))
                    {
                        photoOptimizedLookup.TryGetValue(baseKey + "_web", out match);
                    }

                    if (match != null)
                    {
                        optimizedKey = match;
                        photoOptimizedLookup.Remove(baseKey);
                        photoOptimizedLookup.Remove(baseKey + "_web");
                    }

                    displayOrder++;
                    var state = optimizedKey != null ? MediaProcessingState.Ready : MediaProcessingState.Pending;
                    assets.Add(CreatePhotoAsset(key, optimizedKey, state, displayOrder));
                    existingKeys.Add(key);

                    if (!string.IsNullOrWhiteSpace(optimizedKey))
                    {
                        existingKeys.Add(optimizedKey);
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(_storageOptions.PhotoPrefix))
            {
                await foreach (var key in ListKeysAsync(_storageOptions.PhotoPrefix, cancellationToken).ConfigureAwait(false))
                {
                    if (existingKeys.Contains(key))
                    {
                        continue;
                    }

                    var relative = GetRelativeKey(key, _storageOptions.PhotoPrefix);
                    var baseKey = RemoveExtension(relative);
                    photoOptimizedLookup.Remove(baseKey);
                    photoOptimizedLookup.Remove(baseKey + "_web");

                    displayOrder++;
                    assets.Add(CreatePhotoAsset(key, key, MediaProcessingState.Ready, displayOrder));
                }
            }

            if (assets.Count == 0)
            {
                _logger.LogInformation("No new media assets discovered in S3 bucket {Bucket}.", _storageOptions.BucketName);
                return;
            }

            await _dbContext.MediaAssets.AddRangeAsync(assets, cancellationToken).ConfigureAwait(false);
            await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            _logger.LogInformation("Imported {Count} media asset(s) from S3 bucket {Bucket}.", assets.Count, _storageOptions.BucketName);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Failed to import existing media assets from S3. Skipping bootstrap.");
        }
    }

    private async Task CleanupDuplicateDeliveryAssetsAsync(CancellationToken cancellationToken)
    {
        var assetsByPlayback = await _dbContext.MediaAssets
            .Where(x => x.PlaybackPath != null)
            .GroupBy(x => x.PlaybackPath!)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var toRemove = new List<MediaAsset>();

        foreach (var group in assetsByPlayback)
        {
            if (group.Count() <= 1)
            {
                continue;
            }

            var keep = group.FirstOrDefault(IsOriginalAsset) ?? group.First();

            foreach (var candidate in group)
            {
                if (candidate.Id == keep.Id)
                {
                    continue;
                }

                if (!IsOriginalAsset(candidate))
                {
                    toRemove.Add(candidate);
                }
            }
        }

        if (toRemove.Count > 0)
        {
            _logger.LogInformation("Removing {Count} duplicate delivery media asset(s).", toRemove.Count);
            _dbContext.MediaAssets.RemoveRange(toRemove);
            await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private bool IsOriginalAsset(MediaAsset asset)
    {
        var sourcePath = asset.SourcePath ?? asset.StoragePath ?? string.Empty;

        return asset.AssetType switch
        {
            MediaAssetType.Photo => !string.IsNullOrWhiteSpace(_storageOptions.PhotoSourcePrefix)
                && sourcePath.StartsWith(NormalizePrefix(_storageOptions.PhotoSourcePrefix).TrimEnd('/'), StringComparison.OrdinalIgnoreCase),
            MediaAssetType.Video => !string.IsNullOrWhiteSpace(_storageOptions.VideoSourcePrefix)
                && sourcePath.StartsWith(NormalizePrefix(_storageOptions.VideoSourcePrefix).TrimEnd('/'), StringComparison.OrdinalIgnoreCase),
            _ => true
        };
    }

    private async Task<Dictionary<string, string>> BuildLookupAsync(string prefix, CancellationToken cancellationToken)
    {
        var lookup = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        await foreach (var key in ListKeysAsync(prefix, cancellationToken).ConfigureAwait(false))
        {
            var relative = GetRelativeKey(key, prefix);
            var baseKey = RemoveExtension(relative);
            if (!lookup.ContainsKey(baseKey))
            {
                lookup[baseKey] = key;
            }
        }

        return lookup;
    }

    private MediaAsset CreateVideoAsset(string storageKey, string? playbackKey, string? posterKey, MediaProcessingState state, int displayOrder)
    {
        var title = BuildTitleFromKey(storageKey);
        return new MediaAsset
        {
            Id = Guid.NewGuid(),
            Title = title,
            Description = null,
            AssetType = MediaAssetType.Video,
            StoragePath = storageKey,
            SourcePath = storageKey,
            PlaybackPath = playbackKey,
            PosterPath = posterKey,
            ProcessingState = state,
            IsFeatured = false,
            ShowOnHome = false,
            IsPublished = true,
            DisplayOrder = displayOrder,
            CreatedAt = _clock.GetCurrentInstant()
        };
    }

    private MediaAsset CreatePhotoAsset(string storageKey, string? playbackKey, MediaProcessingState state, int displayOrder)
    {
        var titleSource = playbackKey ?? storageKey;
        var title = BuildTitleFromKey(titleSource);
        var expectedPlayback = BuildPhotoPlaybackPath(storageKey);
        var resolvedPlayback = state == MediaProcessingState.Ready
            ? playbackKey ?? expectedPlayback
            : playbackKey ?? expectedPlayback;

        return new MediaAsset
        {
            Id = Guid.NewGuid(),
            Title = title,
            Description = null,
            AssetType = MediaAssetType.Photo,
            StoragePath = storageKey,
            SourcePath = storageKey,
            PlaybackPath = resolvedPlayback,
            PosterPath = null,
            ProcessingState = state,
            IsFeatured = false,
            ShowOnHome = false,
            IsPublished = true,
            DisplayOrder = displayOrder,
            CreatedAt = _clock.GetCurrentInstant()
        };
    }

    private string BuildPhotoPlaybackPath(string sourceKey)
    {
        if (string.IsNullOrWhiteSpace(_storageOptions.PhotoPrefix))
        {
            return sourceKey;
        }

        var normalized = NormalizePrefix(_storageOptions.PhotoPrefix);
        var prefix = normalized.TrimEnd('/');
        var baseName = BuildSanitizedBaseName(sourceKey);
        var fileName = string.Concat(baseName, "_web.jpg");
        return string.IsNullOrWhiteSpace(prefix) ? fileName : string.Join('/', prefix, fileName);
    }

    private async IAsyncEnumerable<string> ListKeysAsync(string prefix, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var normalizedPrefix = NormalizePrefix(prefix);
        string? continuationToken = null;

        do
        {
            var request = new ListObjectsV2Request
            {
                BucketName = _storageOptions.BucketName,
                Prefix = normalizedPrefix,
                ContinuationToken = continuationToken
            };

            var response = await _s3Client.ListObjectsV2Async(request, cancellationToken).ConfigureAwait(false);
            foreach (var s3Object in response.S3Objects ?? new List<S3Object>())
            {
                var key = s3Object.Key;
                if (string.IsNullOrWhiteSpace(key) || key.EndsWith('/'))
                {
                    continue;
                }

                yield return key;
            }

            continuationToken = response.IsTruncated.GetValueOrDefault() && !string.IsNullOrWhiteSpace(response.NextContinuationToken)
                ? response.NextContinuationToken
                : null;
        }
        while (!string.IsNullOrEmpty(continuationToken));
    }

    private string NormalizePrefix(string prefix)
    {
        if (string.IsNullOrWhiteSpace(prefix))
        {
            return string.Empty;
        }

        var trimmed = prefix.Replace('\\', '/').Trim('/');
        return string.IsNullOrEmpty(trimmed) ? string.Empty : trimmed + "/";
    }

    private string GetRelativeKey(string key, string prefix)
    {
        var normalizedPrefix = NormalizePrefix(prefix);
        if (string.IsNullOrEmpty(normalizedPrefix))
        {
            return key;
        }

        if (key.StartsWith(normalizedPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return key.Substring(normalizedPrefix.Length);
        }

        return key;
    }

    private static string RemoveExtension(string key)
    {
        var index = key.LastIndexOf('.');
        return index >= 0 ? key[..index] : key;
    }

    private static string BuildTitleFromKey(string key)
    {
        var fileName = key.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).LastOrDefault() ?? key;
        var withoutExtension = RemoveExtension(fileName).Replace('-', ' ').Replace('_', ' ');
        return CultureInfo.CurrentCulture.TextInfo.ToTitleCase(withoutExtension);
    }

    private static string BuildSanitizedBaseName(string path)
    {
        var fileName = Path.GetFileNameWithoutExtension(path);
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
