using Amazon.S3;
using Amazon.S3.Model;

namespace GE.BandSite.Server.Services.Storage;

public interface IS3Client : IDisposable
{
    string GeneratePreSignedUrl(GetPreSignedUrlRequest request);

    Task CopyObjectAsync(CopyObjectRequest request, CancellationToken cancellationToken = default);

    Task DeleteObjectAsync(DeleteObjectRequest request, CancellationToken cancellationToken = default);

    Task DownloadObjectAsync(string bucketName, string key, string destinationPath, CancellationToken cancellationToken = default);

    Task UploadObjectAsync(string bucketName, string key, string sourcePath, string contentType, CancellationToken cancellationToken = default);
}
