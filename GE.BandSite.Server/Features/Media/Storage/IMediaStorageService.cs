namespace GE.BandSite.Server.Features.Media.Storage;

public interface IMediaStorageService
{
    Task<PresignedUploadResponse> CreateUploadAsync(MediaUploadRequest request, CancellationToken cancellationToken = default);

    Task<string> PromotePhotoAsync(string rawKey, Guid assetId, string fileName, string contentType, CancellationToken cancellationToken = default);

    Task<string> PromoteVideoSourceAsync(string rawKey, Guid assetId, string fileName, string contentType, CancellationToken cancellationToken = default);

    Task<string> PromotePosterAsync(string rawKey, Guid assetId, string fileName, string contentType, CancellationToken cancellationToken = default);

    Task<string> EnsureLocalCopyAsync(string relativePath, CancellationToken cancellationToken = default);

    Task UploadFromFileAsync(string relativePath, string filePath, string contentType, CancellationToken cancellationToken = default);

    Task DeleteAsync(string relativePath, CancellationToken cancellationToken = default);

    string NormalizeKey(string key);
}
