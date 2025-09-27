using System;
using GE.BandSite.Server.Authentication;

namespace GE.BandSite.Server.Tests.Authentication;

[TestFixture]
public class RefreshTokenGeneratorTests
{
    private RefreshTokenGenerator _generator = null!;

    [SetUp]
    public void SetUp()
    {
        _generator = new RefreshTokenGenerator();
    }

    [Test]
    public void Generate_PopulatesCoreFields()
    {
        var userId = Guid.NewGuid();
        const string ip = "127.0.0.1";
        const string device = "UnitTests";

        var token = _generator.Generate(userId, ip, device);

        Assert.Multiple(() =>
        {
            Assert.That(token.UserId, Is.EqualTo(userId));
            Assert.That(token.IPAddress, Is.EqualTo(ip));
            Assert.That(token.DeviceInfo, Is.EqualTo(device));
            Assert.That(string.IsNullOrWhiteSpace(token.Token), Is.False);
        });

        var duration = token.ExpiresAt - token.CreatedAt;
        Assert.That(duration, Is.EqualTo(NodaTime.Duration.FromDays(AuthenticationConfiguration.RefreshTokenExpirationDays)));
    }

    [Test]
    public void Generate_CalledTwice_ProducesUniqueTokens()
    {
        var first = _generator.Generate(Guid.NewGuid(), "ip", "device");
        var second = _generator.Generate(Guid.NewGuid(), "ip", "device");

        Assert.That(first.Token, Is.Not.EqualTo(second.Token));
    }

    [Test]
    public void Generate_Base64TokenHasExpectedLength()
    {
        var refresh = _generator.Generate(Guid.NewGuid(), "ip", "device");

        Assert.That(refresh.Token.Length, Is.EqualTo(88));
        Assert.That(Convert.FromBase64String(refresh.Token).Length, Is.EqualTo(64));
    }
}
