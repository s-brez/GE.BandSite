using Microsoft.Extensions.Options;

namespace GE.BandSite.Server.Configuration;

/// <summary>
/// Validates <see cref="MediaDeliveryOptions"/> ensuring media delivery is configured to use a single S3/CloudFront base URL.
/// </summary>
public sealed class MediaDeliveryOptionsValidator : IValidateOptions<MediaDeliveryOptions>
{
    public ValidateOptionsResult Validate(string? name, MediaDeliveryOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (string.IsNullOrWhiteSpace(options.BaseUrl))
        {
            return ValidateOptionsResult.Fail("MediaDelivery:BaseUrl must be configured to serve media from S3/CloudFront.");
        }

        if (!Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out var baseUri))
        {
            return ValidateOptionsResult.Fail("MediaDelivery:BaseUrl must be an absolute HTTP(S) URL.");
        }

        if (!string.Equals(baseUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(baseUri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase))
        {
            return ValidateOptionsResult.Fail("MediaDelivery:BaseUrl must use the HTTPS or HTTP scheme.");
        }

        return ValidateOptionsResult.Success;
    }
}
