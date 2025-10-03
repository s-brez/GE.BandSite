using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Threading.Tasks;
using GE.BandSite.Database;
using GE.BandSite.Database.Authentication;
using GE.BandSite.Server.Authentication;
using GE.BandSite.Testing.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.IdentityModel.Tokens;
using SystemClaimTypes = System.Security.Claims.ClaimTypes;

namespace GE.BandSite.Server.Tests.Authentication;

[TestFixture]
[NonParallelizable]
public class JwtTokenValidationMiddlewareTests
{
    private TestPostgresProvider _postgres = null!;
    private GeBandSiteDbContext _db = null!;
    private RefreshTokenGenerator _refreshTokenGenerator = null!;
    private RSASHA512JWTGenerator _jwtGenerator = null!;
    private RsaSecurityTokenValidator _jwtValidator = null!;
    private User _user = null!;

    [SetUp]
    public async Task SetUp()
    {
        _postgres = new TestPostgresProvider();
        await _postgres.InitializeAsync();

        _db = _postgres.CreateDbContext<GeBandSiteDbContext>();
        await _db.Database.EnsureCreatedAsync();

        _refreshTokenGenerator = new RefreshTokenGenerator();

        using var rsa = RSA.Create(2048);
        var parameters = rsa.ExportParameters(true);
        _jwtGenerator = new RSASHA512JWTGenerator(parameters);
        _jwtValidator = new RsaSecurityTokenValidator(parameters);

        _user = new User
        {
            Id = Guid.NewGuid(),
            Email = "user@example.com",
            FirstName = "First",
            LastName = "Last",
            ExternalPositionDescription = "",
            PasswordHash = new byte[] { 1 },
            Salt = new byte[] { 1 },
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

    private DefaultHttpContext CreateHttpContext(string path)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = path;
        context.Connection.RemoteIpAddress = IPAddress.Parse("127.0.0.1");
        context.Request.Headers["User-Agent"] = "UnitTests";
        return context;
    }

    private string CreateExpiredJwt()
    {
        var now = DateTime.UtcNow.AddMinutes(-10);
        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(new[]
            {
                new Claim(SystemClaimTypes.Email, _user.Email),
                new Claim(SystemClaimTypes.NameIdentifier, _user.Id.ToString())
            }),
            Issuer = "localhost",
            Audience = "localhost",
            IssuedAt = now,
            NotBefore = now,
            Expires = now.AddMinutes(1)
        };

        var token = _jwtGenerator.Generate(descriptor);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    [Test]
    public async Task InvokeAsync_WithValidAccessToken_AllowsRequest()
    {
        var context = CreateHttpContext("/");
        context.SetEndpoint(new Endpoint(_ => Task.CompletedTask, new EndpointMetadataCollection(new AuthorizeAttribute()), "secured"));
        string token = _jwtGenerator.GenerateJwt(_user, new HostString("localhost"));
        context.Request.Headers.Append("Cookie", $"{AuthenticationConfiguration.AccessTokenKey}={token}");

        bool nextCalled = false;
        var middleware = new JWTTokenValidationMiddleware(_ => { nextCalled = true; return Task.CompletedTask; }, _refreshTokenGenerator, _jwtGenerator, _jwtValidator);

        await middleware.InvokeAsync(context, _db);

        Assert.Multiple(() =>
        {
            Assert.That(context.User.Identity?.IsAuthenticated, Is.True);
            Assert.That(nextCalled, Is.True);
        });
    }

    [Test]
    public async Task InvokeAsync_WithExpiredAccessTokenAndRefreshToken_IssuesNewTokens()
    {
        var expiredToken = CreateExpiredJwt();
        var refresh = new RefreshToken
        {
            UserId = _user.Id,
            User = _user,
            Token = "refresh-token",
            CreatedAt = NodaTime.SystemClock.Instance.GetCurrentInstant().Minus(NodaTime.Duration.FromMinutes(5)),
            ExpiresAt = NodaTime.SystemClock.Instance.GetCurrentInstant().Plus(NodaTime.Duration.FromDays(1)),
            IPAddress = "127.0.0.1",
            DeviceInfo = "UnitTests"
        };
        _db.RefreshTokens.Add(refresh);
        await _db.SaveChangesAsync();

        var context = CreateHttpContext("/");
        context.SetEndpoint(new Endpoint(_ => Task.CompletedTask, new EndpointMetadataCollection(new AuthorizeAttribute()), "secured"));
        context.Request.Headers.Append("Cookie", $"{AuthenticationConfiguration.AccessTokenKey}={expiredToken}");
        context.Request.Headers.Append("Cookie", $"{AuthenticationConfiguration.RefreshTokenKey}={refresh.Token}");

        bool nextCalled = false;
        var middleware = new JWTTokenValidationMiddleware(_ => { nextCalled = true; return Task.CompletedTask; }, _refreshTokenGenerator, _jwtGenerator, _jwtValidator);

        await middleware.InvokeAsync(context, _db);

        Assert.Multiple(async () =>
        {
            Assert.That(context.User.Identity?.IsAuthenticated, Is.True);
            Assert.That(nextCalled, Is.True);
            Assert.That(context.Response.Headers["Set-Cookie"].Count, Is.EqualTo(2));

            await _db.Entry(refresh).ReloadAsync();
            Assert.That(refresh.RevokedAt, Is.Not.Null);
            Assert.That(refresh.ReplacedByToken, Is.Not.Null);
            Assert.That(await _db.RefreshTokens.CountAsync(), Is.EqualTo(2));
        });
    }

    [Test]
    public async Task InvokeAsync_MissingTokensOnApiRoute_Returns401()
    {
        var context = CreateHttpContext("/api/resource");
        context.SetEndpoint(new Endpoint(_ => Task.CompletedTask, new EndpointMetadataCollection(new AuthorizeAttribute()), "secured-api"));
        bool nextCalled = false;
        var middleware = new JWTTokenValidationMiddleware(_ => { nextCalled = true; return Task.CompletedTask; }, _refreshTokenGenerator, _jwtGenerator, _jwtValidator);

        await middleware.InvokeAsync(context, _db);

        Assert.Multiple(() =>
        {
            Assert.That(context.Response.StatusCode, Is.EqualTo(StatusCodes.Status401Unauthorized));
            Assert.That(nextCalled, Is.False);
        });
    }

    [Test]
    public async Task InvokeAsync_MissingTokensOnPageRoute_RedirectsToLogin()
    {
        var context = CreateHttpContext("/");
        context.SetEndpoint(new Endpoint(_ => Task.CompletedTask, new EndpointMetadataCollection(new AuthorizeAttribute()), "secured"));
        bool nextCalled = false;
        var middleware = new JWTTokenValidationMiddleware(_ => { nextCalled = true; return Task.CompletedTask; }, _refreshTokenGenerator, _jwtGenerator, _jwtValidator);

        await middleware.InvokeAsync(context, _db);

        Assert.Multiple(() =>
        {
            Assert.That(context.Response.StatusCode, Is.EqualTo(StatusCodes.Status302Found));
            Assert.That(context.Response.Headers.Location, Is.EqualTo(AuthenticationConfiguration.LoginPath));
            Assert.That(nextCalled, Is.False);
        });
    }

    [Test]
    public async Task InvokeAsync_MissingTokensOnPageRouteWithoutAuthorize_AllowsRequest()
    {
        var context = CreateHttpContext("/");
        context.SetEndpoint(new Endpoint(_ => Task.CompletedTask, new EndpointMetadataCollection(), "public"));

        bool nextCalled = false;
        var middleware = new JWTTokenValidationMiddleware(_ => { nextCalled = true; return Task.CompletedTask; }, _refreshTokenGenerator, _jwtGenerator, _jwtValidator);

        await middleware.InvokeAsync(context, _db);

        Assert.Multiple(() =>
        {
            Assert.That(context.Response.StatusCode, Is.EqualTo(StatusCodes.Status200OK));
            Assert.That(nextCalled, Is.True);
        });
    }

    [Test]
    public async Task InvokeAsync_EndpointAllowingAnonymous_SkipsAuthentication()
    {
        var context = CreateHttpContext("/public");
        context.SetEndpoint(new Endpoint(_ => Task.CompletedTask, new EndpointMetadataCollection(new AllowAnonymousAttribute()), "anon"));

        bool nextCalled = false;
        var middleware = new JWTTokenValidationMiddleware(_ => { nextCalled = true; return Task.CompletedTask; }, _refreshTokenGenerator, _jwtGenerator, _jwtValidator);

        await middleware.InvokeAsync(context, _db);

        Assert.Multiple(() =>
        {
            Assert.That(nextCalled, Is.True);
            Assert.That(context.User.Identity?.IsAuthenticated, Is.False);
        });
    }

    [Test]
    public async Task TryRefreshAsync_WithExpiredRefreshToken_ReturnsFalse()
    {
        var refresh = new RefreshToken
        {
            UserId = _user.Id,
            User = _user,
            Token = "expired",
            CreatedAt = NodaTime.SystemClock.Instance.GetCurrentInstant().Minus(NodaTime.Duration.FromDays(10)),
            ExpiresAt = NodaTime.SystemClock.Instance.GetCurrentInstant().Minus(NodaTime.Duration.FromDays(1))
        };
        _db.RefreshTokens.Add(refresh);
        await _db.SaveChangesAsync();

        var context = CreateHttpContext("/");
        var middleware = new JWTTokenValidationMiddleware(_ => Task.CompletedTask, _refreshTokenGenerator, _jwtGenerator, _jwtValidator);

        var (refreshed, principal) = await middleware.TryRefreshAsync(refresh.Token, context, _db);

        Assert.Multiple(() =>
        {
            Assert.That(refreshed, Is.False);
            Assert.That(principal, Is.Null);
            Assert.That(context.Response.Headers.ContainsKey("Set-Cookie"), Is.False);
        });
    }

    [Test]
    public async Task TryRefreshAsync_WithValidToken_ReturnsPrincipalAndCookies()
    {
        var refresh = new RefreshToken
        {
            UserId = _user.Id,
            User = _user,
            Token = "valid",
            CreatedAt = NodaTime.SystemClock.Instance.GetCurrentInstant(),
            ExpiresAt = NodaTime.SystemClock.Instance.GetCurrentInstant().Plus(NodaTime.Duration.FromDays(7)),
            IPAddress = "127.0.0.1",
            DeviceInfo = "UnitTests"
        };
        _db.RefreshTokens.Add(refresh);
        await _db.SaveChangesAsync();

        var context = CreateHttpContext("/");
        var middleware = new JWTTokenValidationMiddleware(_ => Task.CompletedTask, _refreshTokenGenerator, _jwtGenerator, _jwtValidator);

        var (refreshed, principal) = await middleware.TryRefreshAsync(refresh.Token, context, _db);

        Assert.Multiple(async () =>
        {
            Assert.That(refreshed, Is.True);
            Assert.That(principal, Is.Not.Null);
            Assert.That(context.Response.Headers["Set-Cookie"].Count, Is.EqualTo(2));

            await _db.Entry(refresh).ReloadAsync();
            Assert.That(refresh.RevokedAt, Is.Not.Null);
            Assert.That(refresh.ReplacedByToken, Is.Not.Null);
            Assert.That(await _db.RefreshTokens.CountAsync(), Is.EqualTo(2));
        });
    }
}
