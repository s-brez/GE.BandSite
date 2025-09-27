namespace GE.BandSite.Server.Configuration;

public sealed class MediaProcessingOptions
{
    public bool Enabled { get; set; } = true;

    public int PollIntervalSeconds { get; set; } = 60;

    public int BatchSize { get; set; } = 3;

    public string OutputDirectory { get; set; } = "wwwroot/media";

    public string? TempDirectory { get; set; }

    public string? FfmpegPath { get; set; } = "ffmpeg";
}
