using GE.BandSite.Database.Media;
using GE.BandSite.Server.Features.Media.Storage;

namespace GE.BandSite.Server.Features.Media.Admin;

public interface IMediaAdminService
{
    Task<PresignedUploadResponse> CreateUploadAsync(MediaUploadRequest request, CancellationToken cancellationToken = default);

    Task<MediaAsset> CreatePhotoAssetAsync(CreatePhotoAssetParameters parameters, CancellationToken cancellationToken = default);

    Task<MediaAsset> CreateVideoAssetAsync(CreateVideoAssetParameters parameters, CancellationToken cancellationToken = default);
}

public sealed record CreatePhotoAssetParameters(
    string Title,
    string RawObjectKey,
    string ContentType,
    string? Description,
    bool IsFeatured,
    bool ShowOnHome,
    bool IsPublished,
    int DisplayOrder);

public sealed record CreateVideoAssetParameters(
    string Title,
    string RawVideoKey,
    string VideoContentType,
    string? Description,
    string? RawPosterKey,
    string? PosterContentType,
    bool IsFeatured,
    bool ShowOnHome,
    bool IsPublished,
    int DisplayOrder);
