namespace GE.BandSite.Server.Features.Media.Models;

public sealed record HomeMediaModel(MediaItem? FeaturedVideo, IReadOnlyList<MediaItem> HighlightPhotos);
