using Microsoft.Extensions.Configuration;

namespace GE.BandSite.Server.Configuration;

public sealed class AwsConfiguration
{
    public string AccessKey { get; set; } = string.Empty;

    public string SecretKey { get; set; } = string.Empty;

    public string Region { get; set; } = string.Empty;

    public static AwsConfiguration FromConfiguration(IConfiguration configuration)
    {
        if (configuration is null)
        {
            throw new ArgumentNullException(nameof(configuration));
        }

        var result = new AwsConfiguration();
        configuration.GetSection("AWS").Bind(result);

        result.AccessKey = FirstNonEmpty(
            result.AccessKey,
            configuration["AWS_SES_ACCESS_KEY_ID"],
            configuration["AWS_ACCESS_KEY_ID"],
            configuration["AWS__AccessKey"]);

        result.SecretKey = FirstNonEmpty(
            result.SecretKey,
            configuration["AWS_SES_SECRET_ACCESS_KEY"],
            configuration["AWS_SECRET_ACCESS_KEY"],
            configuration["AWS__SecretKey"]);

        result.Region = FirstNonEmpty(
            result.Region,
            configuration["AWS_SES_REGION"],
            configuration["AWS_REGION"],
            configuration["AWS_DEFAULT_REGION"],
            configuration["AWS__Region"]);

        Validate(result);
        return result;
    }

    private static string FirstNonEmpty(params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }
        }

        return string.Empty;
    }

    private static void Validate(AwsConfiguration configuration)
    {
        if (string.IsNullOrWhiteSpace(configuration.AccessKey))
        {
            throw new InvalidOperationException("AWS access key is not configured. Set AWS:AccessKey or AWS_SES_ACCESS_KEY_ID.");
        }

        if (string.IsNullOrWhiteSpace(configuration.SecretKey))
        {
            throw new InvalidOperationException("AWS secret key is not configured. Set AWS:SecretKey or AWS_SES_SECRET_ACCESS_KEY.");
        }

        if (string.IsNullOrWhiteSpace(configuration.Region))
        {
            throw new InvalidOperationException("AWS region is not configured. Set AWS:Region or AWS_SES_REGION.");
        }
    }
}
