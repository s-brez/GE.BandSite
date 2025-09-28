using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using GE.BandSite.Server.Configuration;
using GE.BandSite.Server;
using Microsoft.Extensions.Configuration;
using System.IO;
using System.Net;
using System.Net.Http.Headers;

namespace GE.BandSite.Server.Tests.Integration;

[TestFixture]
public static class HomePageMediaIntegrationTests
{
    private const string PromoVideoUrl = "https://swingtheboogie-media.s3.ap-southeast-2.amazonaws.com/videos/STB_PromoMain_Horizontal.mp4";

    [Test]
    [Explicit("Requires valid AWS credentials with access to the promo video S3 object.")]
    public static async Task PromoVideo_S3Object_AllowsRangedDownload()
    {
        var configuration = BuildConfiguration();
        var awsConfig = configuration.GetSection("AWS").Get<AwsConfiguration>()
            ?? throw new InvalidOperationException("AWS configuration is missing. Ensure user secrets are available in the test environment.");

        var credentials = new BasicAWSCredentials(awsConfig.AccessKey, awsConfig.SecretKey);
        var region = RegionEndpoint.GetBySystemName(awsConfig.Region);

        using var s3 = new AmazonS3Client(credentials, region);

        var (bucketName, objectKey) = ParseS3Location(new Uri(PromoVideoUrl));

        var preSignedUrl = s3.GetPreSignedURL(new GetPreSignedUrlRequest
        {
            BucketName = bucketName,
            Key = objectKey,
            Verb = HttpVerb.GET,
            Expires = DateTime.UtcNow.AddMinutes(5)
        });

        using var httpClient = new HttpClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, preSignedUrl);
        request.Headers.Range = new RangeHeaderValue(0, 1023);

        var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);

        if (response.StatusCode == HttpStatusCode.Forbidden)
        {
            var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            Assert.Fail($"Promo video request returned 403 Forbidden. Response body: {body}");
        }

        var contentType = response.Content.Headers.ContentType?.MediaType;
        await using var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
        var buffer = new byte[1024];
        var bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, CancellationToken.None).ConfigureAwait(false);

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.AnyOf(HttpStatusCode.PartialContent, HttpStatusCode.OK));
            Assert.That(bytesRead, Is.GreaterThan(0), "Expected at least one byte from the promo video stream.");
            Assert.That(contentType, Is.EqualTo("video/mp4"));
        });
    }

    private static IConfiguration BuildConfiguration()
    {
        var serverProjectPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "GE.BandSite.Server"));

        return new ConfigurationBuilder()
            .SetBasePath(serverProjectPath)
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddUserSecrets(typeof(Program).Assembly, optional: true)
            .AddEnvironmentVariables()
            .Build();
    }

    private static (string Bucket, string Key) ParseS3Location(Uri uri)
    {
        if (!uri.Host.Contains('.'))
        {
            throw new InvalidOperationException($"Unexpected S3 host format: {uri.Host}");
        }

        var bucket = uri.Host.Split('.', StringSplitOptions.RemoveEmptyEntries)[0];
        var key = uri.AbsolutePath.TrimStart('/');
        return (bucket, key);
    }
}
