namespace GE.BandSite.Server.Features.Media.Models;

public sealed record MediaGalleryModel(IReadOnlyList<MediaItem> Videos, IReadOnlyList<MediaItem> Photos);
