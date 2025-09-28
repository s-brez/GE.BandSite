namespace GE.BandSite.Server.Configuration;

public sealed class MediaProcessingOptions
{
    public bool Enabled { get; set; } = true;

    public int PollIntervalSeconds { get; set; } = 60;

    public int BatchSize { get; set; } = 3;

    public string OutputDirectory { get; set; } = "wwwroot/media";

    public string? TempDirectory { get; set; }

    public string? FfmpegPath { get; set; } = "ffmpeg";

    public string? FfprobePath { get; set; } = "ffprobe";

    /// <summary>
    /// Gets or sets a value indicating whether photo optimization is enabled.
    /// </summary>
    public bool PhotoOptimizationEnabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the maximum width of optimized photos in pixels. Values less than or equal to zero keep the original width.
    /// </summary>
    public int PhotoMaxWidth { get; set; } = 2048;

    /// <summary>
    /// Gets or sets the maximum height of optimized photos in pixels. Values less than or equal to zero keep the original height.
    /// </summary>
    public int PhotoMaxHeight { get; set; } = 2048;

    /// <summary>
    /// Gets or sets the JPEG quality (1-100) applied to optimized photos.
    /// </summary>
    public int PhotoJpegQuality { get; set; } = 85;
}
