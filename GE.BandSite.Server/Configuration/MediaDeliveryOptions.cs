namespace GE.BandSite.Server.Configuration;

public sealed class MediaDeliveryOptions
{
    private string? _baseUrl;

    /// <summary>
    /// Base URL for serving media assets (for example, a CloudFront distribution).
    /// </summary>
    public string? BaseUrl
    {
        get => _baseUrl;
        set => _baseUrl = value?.Trim();
    }
}
