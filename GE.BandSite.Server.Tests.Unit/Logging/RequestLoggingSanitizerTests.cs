using System.Reflection;

namespace GE.BandSite.Server.Tests.Logging;

[TestFixture]
public class RequestLoggingSanitizerTests
{
    private static readonly MethodInfo SanitizeMethod = typeof(Program)
        .GetMethod("SanitizePath", BindingFlags.NonPublic | BindingFlags.Static)
        ?? throw new InvalidOperationException("SanitizePath helper not found");

    [Test]
    public void SanitizePath_WithTrackingToken_ReplacesSequence()
    {
        var input = "/t/01234567-89ab-cdef-0123-456789abcdef";
        var sanitized = (string)SanitizeMethod.Invoke(null, new object[] { input })!;

        Assert.That(sanitized, Does.Not.Contain("01234567-89ab-cdef-0123-456789abcdef"));
        Assert.That(sanitized, Does.Contain("***"));
    }

    [Test]
    public void SanitizePath_WithNormalRoute_ReturnsOriginal()
    {
        var input = "/media/gallery";
        var sanitized = (string)SanitizeMethod.Invoke(null, new object[] { input })!;

        Assert.That(sanitized, Is.EqualTo(input));
    }
}
