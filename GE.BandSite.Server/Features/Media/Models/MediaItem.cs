namespace GE.BandSite.Server.Features.Media.Models;

public sealed record MediaItem(
    Guid Id,
    string Title,
    string? Description,
    string Url,
    string? PosterUrl,
    IReadOnlyList<string> Tags,
    string AssetType);
