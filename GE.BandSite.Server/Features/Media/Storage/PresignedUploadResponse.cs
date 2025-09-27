namespace GE.BandSite.Server.Features.Media.Storage;

public sealed record PresignedUploadResponse(string UploadUrl, string ObjectKey, DateTimeOffset ExpiresAt, string ContentType);
