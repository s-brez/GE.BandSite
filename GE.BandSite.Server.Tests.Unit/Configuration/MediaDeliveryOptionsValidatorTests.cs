using GE.BandSite.Server.Configuration;

namespace GE.BandSite.Server.Tests.Configuration;

[TestFixture]
public class MediaDeliveryOptionsValidatorTests
{
    private MediaDeliveryOptionsValidator _validator = null!;

    [SetUp]
    public void SetUp()
    {
        _validator = new MediaDeliveryOptionsValidator();
    }

    [Test]
    public void Validate_WithValidBaseUrl_ReturnsSuccess()
    {
        var options = new MediaDeliveryOptions { BaseUrl = "https://cdn.example.com/media" };

        var result = _validator.Validate(string.Empty, options);

        Assert.That(result.Succeeded, Is.True);
    }

    [Test]
    public void Validate_WithMissingBaseUrl_ReturnsFailure()
    {
        var result = _validator.Validate(string.Empty, new MediaDeliveryOptions());

        Assert.That(result.Succeeded, Is.False);
    }

    [TestCase("ftp://cdn.example.com")]
    [TestCase("cdn.example.com")]
    public void Validate_WithInvalidBaseUrl_ReturnsFailure(string baseUrl)
    {
        var options = new MediaDeliveryOptions { BaseUrl = baseUrl };

        var result = _validator.Validate(string.Empty, options);

        Assert.That(result.Succeeded, Is.False);
    }

    [Test]
    public void Validate_WithHttpBaseUrl_ReturnsSuccess()
    {
        var options = new MediaDeliveryOptions { BaseUrl = "http://localhost:9000/media" };

        var result = _validator.Validate(string.Empty, options);

        Assert.That(result.Succeeded, Is.True);
    }
}
