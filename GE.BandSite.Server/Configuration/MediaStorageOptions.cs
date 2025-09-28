namespace GE.BandSite.Server.Configuration;

/// <summary>
/// Provides configuration for media storage paths, upload validation, and pre-signed URL behaviour.
/// </summary>
public sealed class MediaStorageOptions
{
    /// <summary>
    /// Gets or sets the S3 bucket name that stores all media assets. When null or empty, the application may fall back to the local file system (for development/testing only).
    /// </summary>
    public string? BucketName { get; set; }

    /// <summary>
    /// Gets or sets the key prefix used for raw uploads awaiting processing (for example <c>uploads/raw</c>).
    /// </summary>
    public string RawUploadPrefix { get; set; } = "uploads/raw";

    /// <summary>
    /// Gets or sets the key prefix used for original photo assets retained for processing.
    /// </summary>
    public string PhotoSourcePrefix { get; set; } = "images/originals";

    /// <summary>
    /// Gets or sets the key prefix used for published photo assets.
    /// </summary>
    public string PhotoPrefix { get; set; } = "media/photos";

    /// <summary>
    /// Gets or sets the key prefix used for original video sources once staged for processing.
    /// </summary>
    public string VideoSourcePrefix { get; set; } = "media/videos/source";

    /// <summary>
    /// Gets or sets the key prefix used for transcoded video playback files.
    /// </summary>
    public string VideoPlaybackPrefix { get; set; } = "media/videos/playback";

    /// <summary>
    /// Gets or sets the key prefix used for video poster imagery.
    /// </summary>
    public string PosterPrefix { get; set; } = "media/posters";

    /// <summary>
    /// Gets or sets the number of minutes that presigned upload URLs remain valid.
    /// </summary>
    public int PresignedExpiryMinutes { get; set; } = 15;

    /// <summary>
    /// When set, the upload pipeline can use the local file system instead of S3. Intended for automated tests and local development scenarios.
    /// </summary>
    public bool EnableLocalDevelopmentFallback { get; set; } = true;

    /// <summary>
    /// Gets or sets the maximum permitted file size (in bytes) for uploaded photos.
    /// </summary>
    public long MaxPhotoBytes { get; set; } = 25_000_000;

    /// <summary>
    /// Gets or sets the maximum permitted file size (in bytes) for uploaded videos.
    /// </summary>
    public long MaxVideoBytes { get; set; } = 2_000_000_000;

    /// <summary>
    /// Gets or sets the maximum permitted file size (in bytes) for uploaded poster images.
    /// </summary>
    public long MaxPosterBytes { get; set; } = 10_000_000;

    /// <summary>
    /// Gets or sets the list of allowed photo MIME types.
    /// </summary>
    public string[] PhotoContentTypes { get; set; } =
    {
        "image/jpeg",
        "image/png",
        "image/webp"
    };

    /// <summary>
    /// Gets or sets the list of allowed video MIME types.
    /// </summary>
    public string[] VideoContentTypes { get; set; } =
    {
        "video/mp4",
        "video/quicktime",
        "video/quicktime; codecs=mov"
    };

    /// <summary>
    /// Gets or sets the list of allowed poster MIME types.
    /// </summary>
    public string[] PosterContentTypes { get; set; } =
    {
        "image/jpeg",
        "image/png"
    };
}
