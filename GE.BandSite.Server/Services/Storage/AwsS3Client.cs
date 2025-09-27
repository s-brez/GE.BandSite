using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;

namespace GE.BandSite.Server.Services.Storage;

public sealed class AwsS3Client : IS3Client
{
    private readonly IAmazonS3 _amazonS3;

    public AwsS3Client(IAmazonS3 amazonS3)
    {
        _amazonS3 = amazonS3;
    }

    public string GeneratePreSignedUrl(GetPreSignedUrlRequest request)
    {
        return _amazonS3.GetPreSignedURL(request);
    }

    public Task CopyObjectAsync(CopyObjectRequest request, CancellationToken cancellationToken = default)
    {
        request.ThrowIfNull();
        return _amazonS3.CopyObjectAsync(request, cancellationToken);
    }

    public Task DeleteObjectAsync(DeleteObjectRequest request, CancellationToken cancellationToken = default)
    {
        request.ThrowIfNull();
        return _amazonS3.DeleteObjectAsync(request, cancellationToken);
    }

    public async Task DownloadObjectAsync(string bucketName, string key, string destinationPath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(bucketName);
        ArgumentException.ThrowIfNullOrEmpty(key);
        ArgumentException.ThrowIfNullOrEmpty(destinationPath);

        using var response = await _amazonS3.GetObjectAsync(bucketName, key, cancellationToken).ConfigureAwait(false);
        Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
        await response.WriteResponseStreamToFileAsync(destinationPath, false, cancellationToken).ConfigureAwait(false);
    }

    public async Task UploadObjectAsync(string bucketName, string key, string sourcePath, string contentType, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(bucketName);
        ArgumentException.ThrowIfNullOrEmpty(key);
        ArgumentException.ThrowIfNullOrEmpty(sourcePath);
        ArgumentException.ThrowIfNullOrEmpty(contentType);

        if (!File.Exists(sourcePath))
        {
            throw new FileNotFoundException("Source file for upload was not found.", sourcePath);
        }

        var request = new PutObjectRequest
        {
            BucketName = bucketName,
            Key = key,
            FilePath = sourcePath,
            ContentType = contentType
        };

        await _amazonS3.PutObjectAsync(request, cancellationToken).ConfigureAwait(false);
    }

    public void Dispose()
    {
        _amazonS3.Dispose();
    }
}

internal static class S3ClientGuardExtensions
{
    public static void ThrowIfNull(this AmazonWebServiceRequest? request)
    {
        if (request == null)
        {
            throw new ArgumentNullException(nameof(request));
        }
    }
}
