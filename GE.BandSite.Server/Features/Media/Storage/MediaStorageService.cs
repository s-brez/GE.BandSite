using Amazon.S3;
using Amazon.S3.Model;
using GE.BandSite.Server.Configuration;
using GE.BandSite.Server.Services.Storage;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GE.BandSite.Server.Features.Media.Storage;

public sealed class MediaStorageService : IMediaStorageService
{
    private readonly MediaStorageOptions _options;
    private readonly MediaStoragePathResolver _pathResolver;
    private readonly IS3Client _s3Client;
    private readonly ILogger<MediaStorageService> _logger;

    public MediaStorageService(IOptions<MediaStorageOptions> options, IWebHostEnvironment environment, IS3Client s3Client, ILogger<MediaStorageService> logger)
    {
        _options = options.Value;
        _pathResolver = new MediaStoragePathResolver(environment);
        _s3Client = s3Client;
        _logger = logger;
    }

    public Task<PresignedUploadResponse> CreateUploadAsync(MediaUploadRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        ValidateUpload(request);

        if (string.IsNullOrWhiteSpace(_options.BucketName))
        {
            throw new InvalidOperationException("Media storage bucket is not configured. Configure MediaStorage:BucketName to enable uploads.");
        }

        var rawPrefix = GetRawPrefix(request.Kind);
        var key = BuildRawKey(rawPrefix, request.FileName);
        var expiry = TimeSpan.FromMinutes(Math.Max(1, _options.PresignedExpiryMinutes));

        var preSignedRequest = new GetPreSignedUrlRequest
        {
            BucketName = _options.BucketName,
            Key = key,
            Verb = HttpVerb.PUT,
            ContentType = request.ContentType,
            Expires = DateTime.UtcNow.Add(expiry)
        };

        var uploadUrl = _s3Client.GeneratePreSignedUrl(preSignedRequest);

        return Task.FromResult(new PresignedUploadResponse(uploadUrl, key, DateTimeOffset.UtcNow.Add(expiry), request.ContentType));
    }

    public async Task<string> PromotePhotoAsync(string rawKey, Guid assetId, string fileName, string contentType, CancellationToken cancellationToken = default)
    {
        var destinationPrefix = string.IsNullOrWhiteSpace(_options.PhotoSourcePrefix)
            ? _options.PhotoPrefix
            : _options.PhotoSourcePrefix;

        destinationPrefix = NormalizeKey(destinationPrefix);

        return await PromoteAsync(rawKey, destinationPrefix, assetId, fileName, contentType, cancellationToken).ConfigureAwait(false);
    }

    public async Task<string> PromoteVideoSourceAsync(string rawKey, Guid assetId, string fileName, string contentType, CancellationToken cancellationToken = default)
    {
        return await PromoteAsync(rawKey, _options.VideoSourcePrefix, assetId, fileName, contentType, cancellationToken).ConfigureAwait(false);
    }

    public async Task<string> PromotePosterAsync(string rawKey, Guid assetId, string fileName, string contentType, CancellationToken cancellationToken = default)
    {
        return await PromoteAsync(rawKey, _options.PosterPrefix, assetId, fileName, contentType, cancellationToken).ConfigureAwait(false);
    }

    public async Task<string> EnsureLocalCopyAsync(string relativePath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);
        var normalized = NormalizeKey(relativePath);
        var physicalPath = _pathResolver.Resolve(normalized);

        if (File.Exists(physicalPath))
        {
            return physicalPath;
        }

        if (string.IsNullOrWhiteSpace(_options.BucketName))
        {
            throw new FileNotFoundException("Media asset not found locally and media storage bucket not configured.", physicalPath);
        }

        Directory.CreateDirectory(Path.GetDirectoryName(physicalPath)!);
        await _s3Client.DownloadObjectAsync(_options.BucketName!, normalized, physicalPath, cancellationToken).ConfigureAwait(false);
        return physicalPath;
    }

    public async Task UploadFromFileAsync(string relativePath, string filePath, string contentType, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentType);

        var normalized = NormalizeKey(relativePath);
        var physicalPath = _pathResolver.Resolve(normalized);
        Directory.CreateDirectory(Path.GetDirectoryName(physicalPath)!);
        File.Copy(filePath, physicalPath, overwrite: true);

        if (!string.IsNullOrWhiteSpace(_options.BucketName))
        {
            await _s3Client.UploadObjectAsync(_options.BucketName!, normalized, filePath, contentType, cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task DeleteAsync(string relativePath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);
        var normalized = NormalizeKey(relativePath);
        var physicalPath = _pathResolver.Resolve(normalized);

        if (File.Exists(physicalPath))
        {
            File.Delete(physicalPath);
        }

        if (!string.IsNullOrWhiteSpace(_options.BucketName))
        {
            await _s3Client.DeleteObjectAsync(new DeleteObjectRequest
            {
                BucketName = _options.BucketName,
                Key = normalized
            }, cancellationToken).ConfigureAwait(false);
        }
    }

    public string NormalizeKey(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        var trimmed = key.Trim();
        trimmed = trimmed.Replace("\\", "/");
        while (trimmed.Contains("//", StringComparison.Ordinal))
        {
            trimmed = trimmed.Replace("//", "/", StringComparison.Ordinal);
        }

        return trimmed.TrimStart('/');
    }

    private async Task<string> PromoteAsync(string rawKey, string destinationPrefix, Guid assetId, string fileName, string contentType, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rawKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationPrefix);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentType);

        var normalizedRaw = NormalizeKey(rawKey);
        var destinationKey = BuildFinalKey(destinationPrefix, assetId, fileName, normalizedRaw);
        var localRawPath = await EnsureLocalCopyAsync(normalizedRaw, cancellationToken).ConfigureAwait(false);
        var localDestinationPath = _pathResolver.Resolve(destinationKey);

        Directory.CreateDirectory(Path.GetDirectoryName(localDestinationPath)!);
        if (!string.Equals(localRawPath, localDestinationPath, StringComparison.OrdinalIgnoreCase))
        {
            File.Copy(localRawPath, localDestinationPath, overwrite: true);
        }

        if (!string.IsNullOrWhiteSpace(_options.BucketName))
        {
            await _s3Client.UploadObjectAsync(_options.BucketName!, destinationKey, localDestinationPath, contentType, cancellationToken).ConfigureAwait(false);
            await _s3Client.DeleteObjectAsync(new DeleteObjectRequest
            {
                BucketName = _options.BucketName,
                Key = normalizedRaw
            }, cancellationToken).ConfigureAwait(false);
        }

        try
        {
            if (!string.Equals(localRawPath, localDestinationPath, StringComparison.OrdinalIgnoreCase) && File.Exists(localRawPath))
            {
                File.Delete(localRawPath);
            }
        }
        catch (IOException exception)
        {
            _logger.LogWarning(exception, "Failed to delete raw media working file {RawPath}", localRawPath);
        }

        return destinationKey;
    }

    private void ValidateUpload(MediaUploadRequest request)
    {
        var allowedTypes = request.Kind switch
        {
            MediaUploadKind.Photo => _options.PhotoContentTypes,
            MediaUploadKind.VideoSource => _options.VideoContentTypes,
            MediaUploadKind.Poster => _options.PosterContentTypes,
            _ => Array.Empty<string>()
        };

        if (allowedTypes.Length > 0 && !allowedTypes.Contains(request.ContentType, StringComparer.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Content type {request.ContentType} is not permitted for {request.Kind} uploads.");
        }

        var lengthLimit = request.Kind switch
        {
            MediaUploadKind.Photo => _options.MaxPhotoBytes,
            MediaUploadKind.VideoSource => _options.MaxVideoBytes,
            MediaUploadKind.Poster => _options.MaxPosterBytes,
            _ => _options.MaxPhotoBytes
        };

        if (lengthLimit > 0 && request.ContentLength > lengthLimit)
        {
            throw new InvalidOperationException($"Upload exceeds the maximum allowed size of {lengthLimit} bytes.");
        }
    }

    private string GetRawPrefix(MediaUploadKind kind)
    {
        var root = NormalizeKey(_options.RawUploadPrefix);
        var suffix = kind switch
        {
            MediaUploadKind.Photo => "photos",
            MediaUploadKind.VideoSource => "videos",
            MediaUploadKind.Poster => "posters",
            _ => "misc"
        };

        return Combine(root, suffix);
    }

    private string BuildRawKey(string rawPrefix, string fileName)
    {
        var now = DateTime.UtcNow;
        var basePath = Combine(rawPrefix, now.Year.ToString("D4", System.Globalization.CultureInfo.InvariantCulture), now.Month.ToString("D2", System.Globalization.CultureInfo.InvariantCulture));
        var safeFileName = BuildSafeFileName(fileName);
        return Combine(basePath, safeFileName);
    }

    private string BuildFinalKey(string prefix, Guid assetId, string fileName, string? fallbackRawKey = null)
    {
        var baseName = string.IsNullOrWhiteSpace(fileName)
            ? fallbackRawKey ?? assetId.ToString("N", System.Globalization.CultureInfo.InvariantCulture)
            : fileName;

        var safeBase = BuildSafeFileName(baseName);
        var extension = Path.GetExtension(safeBase);
        var cleanName = Path.GetFileNameWithoutExtension(safeBase);
        if (string.IsNullOrWhiteSpace(cleanName))
        {
            cleanName = assetId.ToString("N", System.Globalization.CultureInfo.InvariantCulture);
        }

        var finalName = string.Concat(cleanName, extension);

        var timestamp = DateTime.UtcNow;
        var dated = Combine(prefix, timestamp.Year.ToString("D4", System.Globalization.CultureInfo.InvariantCulture), timestamp.Month.ToString("D2", System.Globalization.CultureInfo.InvariantCulture));
        return Combine(dated, finalName);
    }

    private string BuildSafeFileName(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return $"upload-{Guid.NewGuid():N}";
        }

        var name = Path.GetFileName(fileName);
        var extension = Path.GetExtension(name).ToLowerInvariant();
        var withoutExtension = Path.GetFileNameWithoutExtension(name);
        if (string.IsNullOrWhiteSpace(withoutExtension))
        {
            withoutExtension = "file";
        }

        var safeBase = new string(withoutExtension
            .Select(ch => char.IsLetterOrDigit(ch) ? ch : '-')
            .ToArray());

        while (safeBase.Contains("--", StringComparison.Ordinal))
        {
            safeBase = safeBase.Replace("--", "-", StringComparison.Ordinal);
        }

        safeBase = safeBase.Trim('-');
        if (safeBase.Length == 0)
        {
            safeBase = "file";
        }

        return string.Concat(safeBase, extension);
    }

    private string Combine(params string[] segments)
    {
        return NormalizeKey(string.Join('/', segments.Where(s => !string.IsNullOrWhiteSpace(s))));
    }
}

internal sealed class MediaStoragePathResolver
{
    private readonly string _webRoot;

    public MediaStoragePathResolver(IWebHostEnvironment environment)
    {
        _webRoot = environment.WebRootPath;
        if (string.IsNullOrWhiteSpace(_webRoot))
        {
            _webRoot = Path.Combine(environment.ContentRootPath, "wwwroot");
        }
    }

    public string Resolve(string relativePath)
    {
        var path = relativePath.Replace('/', Path.DirectorySeparatorChar);
        return Path.Combine(_webRoot, path);
    }
}
