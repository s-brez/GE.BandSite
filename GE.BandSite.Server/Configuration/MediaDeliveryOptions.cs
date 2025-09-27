namespace GE.BandSite.Server.Configuration;

public sealed class MediaDeliveryOptions
{
    /// <summary>
    /// Optional base URL for serving media assets (for example, a CloudFront distribution).
    /// </summary>
    public string? BaseUrl { get; set; }
}
