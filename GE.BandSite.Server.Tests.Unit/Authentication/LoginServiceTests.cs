using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using GE.BandSite.Database;
using GE.BandSite.Server.Authentication;
using GE.BandSite.Server.Services;
using GE.BandSite.Testing.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;

namespace GE.BandSite.Server.Tests.Authentication;

[TestFixture]
[NonParallelizable]
public class LoginServiceTests
{
    private TestPostgresProvider _postgres = null!;
    private GeBandSiteDbContext _db = null!;
    private PBKDF2SHA512PasswordHasher _passwordHasher = null!;
    private RefreshTokenGenerator _refreshTokenGenerator = null!;
    private RSASHA512JWTGenerator _jwtGenerator = null!;
    private RsaSecurityTokenValidator _jwtValidator = null!;
    private LoginService _service = null!;
    private User _user = null!;

    [SetUp]
    public async Task SetUp()
    {
        _postgres = new TestPostgresProvider();
        await _postgres.InitializeAsync();

        _db = _postgres.CreateDbContext<GeBandSiteDbContext>();
        await _db.Database.EnsureCreatedAsync();

        _passwordHasher = new PBKDF2SHA512PasswordHasher();
        _refreshTokenGenerator = new RefreshTokenGenerator();
        using var rsa = RSA.Create(2048);
        var parameters = rsa.ExportParameters(true);
        _jwtGenerator = new RSASHA512JWTGenerator(parameters);
        _jwtValidator = new RsaSecurityTokenValidator(parameters);

        _service = new LoginService(
            _db,
            NullLogger<LoginService>.Instance,
            _passwordHasher,
            _refreshTokenGenerator,
            _jwtGenerator);

        var salt = _passwordHasher.GenerateSalt();
        _user = new User
        {
            Id = Guid.NewGuid(),
            Email = "user@example.com",
            FirstName = "First",
            LastName = "Last",
            ExternalPositionDescription = "",
            Salt = salt,
            PasswordHash = _passwordHasher.Hash("StrongPass123!", salt),
            IsActive = true,
            PreviousPasswordHashes = new List<byte[]>()
        };
        _db.Users.Add(_user);
        await _db.SaveChangesAsync();
    }

    [TearDown]
    public async Task TearDown()
    {
        if (_db != null)
        {
            await _db.DisposeAsync();
            await _postgres.DisposeDbContextAsync(_db);
        }

        if (_postgres != null)
        {
            await _postgres.DisposeAsync();
        }
    }

    private static DefaultHttpContext CreateHttpContext()
    {
        var context = new DefaultHttpContext();
        context.Request.Host = new HostString("localhost");
        context.Connection.RemoteIpAddress = IPAddress.Parse("127.0.0.1");
        context.Request.Headers["User-Agent"] = "UnitTests";
        return context;
    }

    [Test]
    public async Task AuthenticateAsync_WithValidCredentials_IssuesTokens()
    {
        var httpContext = CreateHttpContext();

        var result = await _service.AuthenticateAsync(_user.Email, "StrongPass123!", httpContext);

        Assert.Multiple(() =>
        {
            Assert.That(result.Success, Is.True);
            Assert.That(httpContext.Response.Cookies, Is.Not.Null);
            Assert.That(httpContext.Response.Headers["Set-Cookie"].Count, Is.EqualTo(2));
        });

        Assert.That(await _db.RefreshTokens.CountAsync(), Is.EqualTo(1));

        var cookies = httpContext.Response.Headers["Set-Cookie"].ToArray();
        Assert.That(cookies, Is.Not.Empty, "No cookies were issued.");

        var accessCookie = cookies.FirstOrDefault(h => h?.Contains(AuthenticationConfiguration.AccessTokenKey) == true)
            ?? throw new AssertionException("Access token cookie was not issued.");

        var cookieSegment = accessCookie.Split(';', 2)[0];
        var separatorIndex = cookieSegment.IndexOf('=');
        if (separatorIndex < 0)
        {
            throw new AssertionException("Access token cookie is malformed.");
        }

        var tokenValue = cookieSegment.Substring(separatorIndex + 1);
        var validated = _jwtValidator.Validate(tokenValue);
        Assert.That(validated, Is.Not.Null);
    }

    [Test]
    public async Task AuthenticateAsync_WithInvalidPassword_ReturnsUnauthorized()
    {
        var httpContext = CreateHttpContext();

        var result = await _service.AuthenticateAsync(_user.Email, "wrong", httpContext);

        Assert.Multiple(() =>
        {
            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorStatus, Is.EqualTo(StatusCodes.Status401Unauthorized));
            Assert.That(httpContext.Response.Headers.ContainsKey("Set-Cookie"), Is.False);
        });
    }

    [Test]
    public async Task AuthenticateAsync_UserInactive_ReturnsUnauthorized()
    {
        _user.IsActive = false;
        _db.Update(_user);
        await _db.SaveChangesAsync();

        var httpContext = CreateHttpContext();
        var result = await _service.AuthenticateAsync(_user.Email, "StrongPass123!", httpContext);

        Assert.That(result.Success, Is.False);
        Assert.That(result.ErrorStatus, Is.EqualTo(StatusCodes.Status401Unauthorized));
    }

    [Test]
    public async Task AuthenticateAsync_MissingParameters_ReturnsBadRequest()
    {
        var httpContext = CreateHttpContext();

        var result = await _service.AuthenticateAsync(string.Empty, string.Empty, httpContext);

        Assert.That(result.Success, Is.False);
        Assert.That(result.ErrorStatus, Is.EqualTo(StatusCodes.Status400BadRequest));
    }
}
