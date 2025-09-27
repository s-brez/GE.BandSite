using GE.BandSite.Server.Features.Media.Models;

namespace GE.BandSite.Server.Features.Media;

public interface IMediaQueryService
{
    Task<HomeMediaModel> GetHomeHighlightsAsync(CancellationToken cancellationToken = default);

    Task<MediaGalleryModel> GetGalleryAsync(CancellationToken cancellationToken = default);
}
