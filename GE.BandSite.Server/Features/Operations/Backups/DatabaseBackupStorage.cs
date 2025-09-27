using Amazon.S3;
using Amazon.S3.Model;

namespace GE.BandSite.Server.Features.Operations.Backups;

public sealed record DatabaseBackupDescriptor(string Key, DateTimeOffset LastModified);

public interface IDatabaseBackupStorage
{
    Task UploadAsync(string bucketName, string key, string filePath, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DatabaseBackupDescriptor>> ListAsync(string bucketName, string keyPrefix, CancellationToken cancellationToken = default);

    Task DeleteAsync(string bucketName, string key, CancellationToken cancellationToken = default);
}

public sealed class S3DatabaseBackupStorage : IDatabaseBackupStorage
{
    private readonly IAmazonS3 _s3;
    private readonly ILogger<S3DatabaseBackupStorage> _logger;

    public S3DatabaseBackupStorage(IAmazonS3 s3, ILogger<S3DatabaseBackupStorage> logger)
    {
        _s3 = s3;
        _logger = logger;
    }

    public async Task UploadAsync(string bucketName, string key, string filePath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(bucketName);
        ArgumentException.ThrowIfNullOrEmpty(key);
        ArgumentException.ThrowIfNullOrEmpty(filePath);

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException("Backup file not found for upload.", filePath);
        }

        var putRequest = new PutObjectRequest
        {
            BucketName = bucketName,
            Key = key,
            FilePath = filePath,
            ContentType = "application/octet-stream"
        };

        _logger.LogInformation("Uploading database backup to s3://{Bucket}/{Key}.", bucketName, key);
        await _s3.PutObjectAsync(putRequest, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<DatabaseBackupDescriptor>> ListAsync(string bucketName, string keyPrefix, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(bucketName);
        ArgumentNullException.ThrowIfNull(keyPrefix);

        var descriptors = new List<DatabaseBackupDescriptor>();
        var request = new ListObjectsV2Request
        {
            BucketName = bucketName,
            Prefix = keyPrefix
        };

        ListObjectsV2Response response;
        do
        {
            response = await _s3.ListObjectsV2Async(request, cancellationToken).ConfigureAwait(false);
            foreach (var obj in response.S3Objects)
            {
                descriptors.Add(new DatabaseBackupDescriptor(obj.Key, obj?.LastModified ?? DateTimeOffset.Now));
            }

            request.ContinuationToken = response.NextContinuationToken;
        }
        while (response.IsTruncated ?? false);

        return descriptors;
    }

    public async Task DeleteAsync(string bucketName, string key, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(bucketName);
        ArgumentException.ThrowIfNullOrEmpty(key);

        _logger.LogInformation("Deleting expired database backup s3://{Bucket}/{Key}.", bucketName, key);
        var deleteRequest = new DeleteObjectRequest
        {
            BucketName = bucketName,
            Key = key
        };

        await _s3.DeleteObjectAsync(deleteRequest, cancellationToken).ConfigureAwait(false);
    }
}
