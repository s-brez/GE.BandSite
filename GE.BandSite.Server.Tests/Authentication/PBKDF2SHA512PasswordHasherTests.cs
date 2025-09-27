using System;
using System.Linq;
using GE.BandSite.Server.Authentication;

namespace GE.BandSite.Server.Tests.Authentication;

[TestFixture]
public class PBKDF2SHA512PasswordHasherTests
{
    private PBKDF2SHA512PasswordHasher _hasher = null!;

    [SetUp]
    public void SetUp()
    {
        _hasher = new PBKDF2SHA512PasswordHasher();
    }

    [Test]
    public void GenerateSalt_UsesDefaultLength()
    {
        var salt = _hasher.GenerateSalt();

        Assert.That(salt.Length, Is.EqualTo(32));
    }

    [Test]
    public void GenerateSalt_CustomLength_ReturnsRequestedSize()
    {
        var salt = _hasher.GenerateSalt(48);

        Assert.That(salt.Length, Is.EqualTo(48));
    }

    [Test]
    public void GenerateSalt_ProducesRandomBytes()
    {
        var first = _hasher.GenerateSalt();
        var second = _hasher.GenerateSalt();

        Assert.That(first.SequenceEqual(second), Is.False);
    }

    [Test]
    public void Hash_RepeatedCallWithSameInputs_YieldsStableHash()
    {
        var salt = _hasher.GenerateSalt();

        var first = _hasher.Hash("P@ssword123", salt);
        var second = _hasher.Hash("P@ssword123", salt);

        Assert.Multiple(() =>
        {
            Assert.That(first.Length, Is.EqualTo(64));
            Assert.That(second, Is.EqualTo(first));
        });
    }

    [Test]
    public void Hash_WithEmptyPassword_Throws()
    {
        var salt = _hasher.GenerateSalt();

        Assert.Throws<ArgumentNullException>(() => _hasher.Hash(string.Empty, salt));
    }

    [Test]
    public void Hash_WithNullSalt_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => _hasher.Hash("password", null!));
    }

    [Test]
    public void Hash_WithEmptySalt_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => _hasher.Hash("password", Array.Empty<byte>()));
    }
}
