using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Security.Cryptography;
using GE.BandSite.Database;
using GE.BandSite.Server.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.IdentityModel.Tokens;
using ServerClaimTypes = GE.BandSite.Server.Authentication.ClaimTypes;
using SystemClaimTypes = System.Security.Claims.ClaimTypes;

namespace GE.BandSite.Server.Tests.Authentication;

[TestFixture]
public class RsaSecurityTokenValidatorTests
{
    private RSAParameters _parameters;
    private string _token = null!;

    [SetUp]
    public void SetUp()
    {
        using var rsa = RSA.Create(2048);
        _parameters = rsa.ExportParameters(true);

        var tokenHandler = new JwtSecurityTokenHandler();
        var securityKey = new RsaSecurityKey(RSA.Create(_parameters));
        var signingCredentials = new SigningCredentials(securityKey, SecurityAlgorithms.RsaSha512);

        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(new[] { new Claim(SystemClaimTypes.NameIdentifier, "user-1") }),
            Expires = DateTime.UtcNow.AddMinutes(10),
            SigningCredentials = signingCredentials
        };

        var token = tokenHandler.CreateToken(descriptor);
        _token = tokenHandler.WriteToken(token);
    }

    [Test]
    public void Constructor_WithParameters_Succeeds()
    {
        var validator = new RsaSecurityTokenValidator(_parameters);
        Assert.That(validator, Is.Not.Null);
    }

    [Test]
    public void Validate_WithValidToken_ReturnsJwt()
    {
        var validator = new RsaSecurityTokenValidator(_parameters);

        var validated = validator.Validate(_token);

        Assert.That(validated, Is.InstanceOf<JwtSecurityToken>());
    }

    [Test]
    public void Validate_WithInvalidToken_ReturnsNull()
    {
        var validator = new RsaSecurityTokenValidator(_parameters);

        var validated = validator.Validate(_token + "invalid");

        Assert.That(validated, Is.Null);
    }

    [Test]
    public void Validate_WithDifferentKey_ReturnsNull()
    {
        using var other = RSA.Create(2048);
        var validator = new RsaSecurityTokenValidator(other.ExportParameters(true));

        Assert.That(validator.Validate(_token), Is.Null);
    }
}

[TestFixture]
public class RsaSha512JwtGeneratorTests
{
    private RSAParameters _parameters;

    [SetUp]
    public void SetUp()
    {
        using var rsa = RSA.Create(2048);
        _parameters = rsa.ExportParameters(true);
    }

    [Test]
    public void Constructor_WithParameters_Succeeds()
    {
        var generator = new RSASHA512JWTGenerator(_parameters);
        Assert.That(generator, Is.Not.Null);
    }

    [Test]
    public void Generate_ReturnsSignedJwtToken()
    {
        var generator = new RSASHA512JWTGenerator(_parameters);

        var token = generator.Generate();

        Assert.That(token, Is.InstanceOf<JwtSecurityToken>());
        Assert.That(((JwtSecurityToken)token).SignatureAlgorithm, Is.EqualTo(SecurityAlgorithms.RsaSha512));
    }

    [Test]
    public void Generate_WithDescriptor_PreservesClaims()
    {
        var generator = new RSASHA512JWTGenerator(_parameters);
        var descriptor = new SecurityTokenDescriptor
        {
            Audience = "aud",
            Issuer = "iss",
            Expires = DateTime.UtcNow.AddMinutes(5),
            Subject = new ClaimsIdentity(new[]
            {
                new Claim(SystemClaimTypes.NameIdentifier, "user-1"),
                new Claim(SystemClaimTypes.Email, "user@example.com")
            })
        };

        var token = (JwtSecurityToken)generator.Generate(descriptor);

        Assert.Multiple(() =>
        {
            Assert.That(token.Audiences.FirstOrDefault(), Is.EqualTo("aud"));
            Assert.That(token.Issuer, Is.EqualTo("iss"));
            Assert.That(token.Claims.Any(c => c.Type == JwtRegisteredClaimNames.Email && c.Value == "user@example.com"), Is.True);
        });
    }

    [Test]
    public void GenerateJwt_FromUser_PopulatesStandardClaims()
    {
        var generator = new RSASHA512JWTGenerator(_parameters);
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "user@example.com",
            FirstName = "First",
            LastName = "Last",
            ExternalPositionDescription = "",
            PreviousPasswordHashes = new List<byte[]>(),
            PasswordHash = new byte[] { 1 },
            Salt = new byte[] { 1 },
            IsActive = true
        };

        var token = generator.GenerateJwt(user, new HostString("localhost"));

        var validator = new RsaSecurityTokenValidator(_parameters);
        var validated = (JwtSecurityToken?)validator.Validate(token);

        Assert.That(validated, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(validated!.Claims.Any(c => c.Type == ServerClaimTypes.Email && c.Value == user.Email), Is.True);
            Assert.That(validated.Claims.Any(c => c.Type == ServerClaimTypes.FirstName && c.Value == user.FirstName), Is.True);
            Assert.That(validated.Claims.Any(c => c.Type == ServerClaimTypes.LastName && c.Value == user.LastName), Is.True);
            Assert.That(validated.Claims.Any(c => c.Type == ServerClaimTypes.UserId && c.Value == user.Id.ToString()), Is.True);
        });
        Assert.That(validated.Claims.Any(c => c.Type == SystemClaimTypes.Name && c.Value == user.FirstName), Is.False);
        Assert.That(validated.Claims.Any(c => c.Type == SystemClaimTypes.GivenName && c.Value == user.FirstName), Is.False);
        Assert.That(validated.Claims.Any(c => c.Type == ServerClaimTypes.FirstName && c.Value == user.FirstName), Is.True);
    }
}
