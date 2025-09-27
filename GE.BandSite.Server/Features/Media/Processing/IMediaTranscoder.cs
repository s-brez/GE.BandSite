namespace GE.BandSite.Server.Features.Media.Processing;

public interface IMediaTranscoder
{
    Task<MediaTranscodeResult> TranscodeAsync(MediaTranscodeRequest request, CancellationToken cancellationToken = default);
}

public sealed record MediaTranscodeRequest(string InputPath, string OutputPath);

public sealed record MediaTranscodeResult(int? DurationSeconds, int? Width, int? Height);

public static class MediaTranscodeResultDefaults
{
    public static readonly MediaTranscodeResult Empty = new(null, null, null);
}
