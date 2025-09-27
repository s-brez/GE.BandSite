namespace GE.BandSite.Server.Features.Media.Storage;

public sealed record MediaUploadRequest(MediaUploadKind Kind, string FileName, string ContentType, long ContentLength);
