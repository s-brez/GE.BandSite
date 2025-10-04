using System.Security.Cryptography;
using Amazon.SimpleEmailV2.Model;
using GE.BandSite.Database;
using GE.BandSite.Database.Authentication;
using GE.BandSite.Server.Authentication;
using GE.BandSite.Server.Features.Contact;
using GE.BandSite.Server.Services;
using GE.BandSite.Testing.Core;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NodaTime;

namespace GE.BandSite.Server.Tests.Authentication;

[TestFixture]
[NonParallelizable]
public class PasswordResetServiceTests
{
    private const string InitialPassword = "ValidPasscode123!";
    private TestPostgresProvider _postgres = null!;
    private GeBandSiteDbContext _dbContext = null!;
    private TestClock _clock = null!;
    private FakeSesEmailClient _sesClient = null!;
    private PBKDF2SHA512PasswordHasher _passwordHasher = null!;
    private PasswordValidator _passwordValidator = null!;
    private PasswordResetService _service = null!;
    private User _user = null!;
    private PasswordResetOptions _options = null!;
    private byte[] _originalHash = null!;

    [SetUp]
    public async Task SetUp()
    {
        _postgres = new TestPostgresProvider();
        await _postgres.InitializeAsync();

        _dbContext = _postgres.CreateDbContext<GeBandSiteDbContext>();
        await _dbContext.Database.EnsureCreatedAsync();

        _clock = new TestClock(Instant.FromUtc(2025, 1, 1, 12, 0));
        _sesClient = new FakeSesEmailClient();
        _passwordHasher = new PBKDF2SHA512PasswordHasher();
        _passwordValidator = new PasswordValidator();

        _options = new PasswordResetOptions
        {
            Enabled = true,
            FromAddress = "no-reply@example.com",
            ResetLinkBaseUrl = "https://example.com/ResetPassword",
            Subject = "Reset password",
            ExpiryMinutes = 60
        };

        _service = new PasswordResetService(
            _dbContext,
            _sesClient,
            _passwordHasher,
            _passwordValidator,
            _clock,
            Options.Create(_options),
            NullLogger<PasswordResetService>.Instance);

        var salt = _passwordHasher.GenerateSalt();
        _originalHash = _passwordHasher.Hash(InitialPassword, salt);

        _user = new User
        {
            Id = Guid.NewGuid(),
            Email = "admin@example.com",
            FirstName = "Test",
            LastName = "Admin",
            ExternalPositionDescription = "",
            Salt = salt,
            PasswordHash = _originalHash.ToArray(),
            IsActive = true,
            PreviousPasswordHashes = new List<byte[]>()
        };

        _dbContext.Users.Add(_user);
        await _dbContext.SaveChangesAsync();
    }

    [TearDown]
    public async Task TearDown()
    {
        if (_dbContext != null)
        {
            await _dbContext.DisposeAsync();
            await _postgres.DisposeDbContextAsync(_dbContext);
        }

        if (_postgres != null)
        {
            await _postgres.DisposeAsync();
        }
    }

    [Test]
    public async Task RequestPasswordResetAsync_WithValidUser_SendsEmailAndStoresToken()
    {
        var result = await _service.RequestPasswordResetAsync(_user.Email);

        Assert.Multiple(() =>
        {
            Assert.That(result.Accepted, Is.True);
            Assert.That(result.EmailDispatched, Is.True);
            Assert.That(_sesClient.Requests, Has.Count.EqualTo(1));
        });

        var resetRequest = await _dbContext.PasswordResetRequests.SingleAsync();
        Assert.That(resetRequest.UserId, Is.EqualTo(_user.Id));
        Assert.That(resetRequest.ExpiresAt, Is.EqualTo(_clock.GetCurrentInstant().Plus(Duration.FromMinutes(_options.ExpiryMinutes))));

        var token = ExtractToken(_sesClient.Requests.Single());
        var tokenHash = Convert.ToHexString(SHA256.HashData(WebEncoders.Base64UrlDecode(token)));
        Assert.That(resetRequest.TokenHash, Is.EqualTo(tokenHash));
    }

    [Test]
    public async Task RequestPasswordResetAsync_WithUnknownEmail_DoesNotSendEmail()
    {
        var result = await _service.RequestPasswordResetAsync("nobody@example.com");

        Assert.Multiple(() =>
        {
            Assert.That(result.Accepted, Is.True);
            Assert.That(result.EmailDispatched, Is.False);
            Assert.That(_sesClient.Requests, Is.Empty);
        });

        Assert.That(await _dbContext.PasswordResetRequests.CountAsync(), Is.EqualTo(0));
    }

    [Test]
    public async Task ResetPasswordAsync_WithValidToken_UpdatesPasswordAndRevokesRefreshTokens()
    {
        await _service.RequestPasswordResetAsync(_user.Email);
        var token = ExtractToken(_sesClient.Requests.Single());

        var refreshToken = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = _user.Id,
            Token = Guid.NewGuid().ToString("N"),
            CreatedAt = _clock.GetCurrentInstant(),
            ExpiresAt = _clock.GetCurrentInstant().Plus(Duration.FromDays(7))
        };

        _dbContext.RefreshTokens.Add(refreshToken);
        await _dbContext.SaveChangesAsync();

        _clock.Advance(Duration.FromMinutes(5));

        var result = await _service.ResetPasswordAsync(token, "ChangedPass123!!");

        Assert.That(result.Success, Is.True);

        var updatedUser = await _dbContext.Users.SingleAsync(x => x.Id == _user.Id);
        Assert.Multiple(() =>
        {
            Assert.That(updatedUser.PasswordHash.SequenceEqual(_originalHash), Is.False);
            Assert.That(updatedUser.PasswordChangeDateTime, Is.EqualTo(_clock.GetCurrentInstant()));
            Assert.That(updatedUser.PreviousPasswordHashes, Has.Count.EqualTo(1));
        });

        var updatedRefreshToken = await _dbContext.RefreshTokens.SingleAsync();
        Assert.That(updatedRefreshToken.RevokedAt, Is.Not.Null);

        var resetRequest = await _dbContext.PasswordResetRequests.SingleAsync();
        Assert.That(resetRequest.ConsumedAt, Is.EqualTo(_clock.GetCurrentInstant()));
    }

    [Test]
    public async Task ResetPasswordAsync_WithExpiredToken_ReturnsExpired()
    {
        await _service.RequestPasswordResetAsync(_user.Email);
        var token = ExtractToken(_sesClient.Requests.Single());

        _clock.Advance(Duration.FromMinutes(_options.ExpiryMinutes + 5));

        var result = await _service.ResetPasswordAsync(token, "AnotherPass123!!");

        Assert.Multiple(() =>
        {
            Assert.That(result.Success, Is.False);
            Assert.That(result.Error, Is.EqualTo(PasswordResetError.Expired));
        });

        var resetRequest = await _dbContext.PasswordResetRequests.SingleAsync();
        Assert.That(resetRequest.ConsumedAt, Is.Null);
    }

    [Test]
    public async Task ResetPasswordAsync_WithInvalidPassword_ReturnsValidationErrors()
    {
        await _service.RequestPasswordResetAsync(_user.Email);
        var token = ExtractToken(_sesClient.Requests.Single());

        var result = await _service.ResetPasswordAsync(token, "short");

        Assert.Multiple(() =>
        {
            Assert.That(result.Success, Is.False);
            Assert.That(result.Error, Is.EqualTo(PasswordResetError.PasswordValidationFailed));
            Assert.That(result.PasswordValidationErrors, Is.Not.Empty);
        });

        var resetRequest = await _dbContext.PasswordResetRequests.SingleAsync();
        Assert.That(resetRequest.ConsumedAt, Is.Null);
    }

    [Test]
    public async Task ResetPasswordAsync_WithExistingPassword_ReturnsMismatchAndAllowsRetry()
    {
        await _service.RequestPasswordResetAsync(_user.Email);
        var token = ExtractToken(_sesClient.Requests.Single());

        var firstAttempt = await _service.ResetPasswordAsync(token, InitialPassword);
        Assert.Multiple(() =>
        {
            Assert.That(firstAttempt.Success, Is.False);
            Assert.That(firstAttempt.Error, Is.EqualTo(PasswordResetError.PasswordMatchesCurrent));
        });

        var secondAttempt = await _service.ResetPasswordAsync(token, "UniquePass123!!A");
        Assert.That(secondAttempt.Success, Is.True);
    }

    [Test]
    public async Task ResetPasswordAsync_ReusingToken_ReturnsAlreadyUsed()
    {
        await _service.RequestPasswordResetAsync(_user.Email);
        var token = ExtractToken(_sesClient.Requests.Single());

        var firstAttempt = await _service.ResetPasswordAsync(token, "FreshPassword123!!");
        Assert.That(firstAttempt.Success, Is.True);

        var secondAttempt = await _service.ResetPasswordAsync(token, "AnotherPassword123!!");
        Assert.Multiple(() =>
        {
            Assert.That(secondAttempt.Success, Is.False);
            Assert.That(secondAttempt.Error, Is.EqualTo(PasswordResetError.AlreadyUsed));
        });
    }

    private static string ExtractToken(SendEmailRequest request)
    {
        var body = request.Content.Simple?.Body?.Text?.Data
            ?? throw new AssertionException("Email body not populated");

        var tokenLine = body
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault(line => line.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            ?? throw new AssertionException("Reset link not found in email body.");

        var uri = new Uri(tokenLine, UriKind.Absolute);
        var query = uri.Query.TrimStart('?');
        foreach (var segment in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var pair = segment.Split('=', 2);
            if (pair.Length == 2 && pair[0] == "token")
            {
                return Uri.UnescapeDataString(pair[1]);
            }
        }

        throw new AssertionException("Token parameter missing from reset link.");
    }

    private sealed class FakeSesEmailClient : ISesEmailClient
    {
        public List<SendEmailRequest> Requests { get; } = new();

        public Task<SendEmailResponse> SendEmailAsync(SendEmailRequest request, CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            return Task.FromResult(new SendEmailResponse { MessageId = Guid.NewGuid().ToString() });
        }
    }

    private sealed class TestClock : IClock
    {
        private Instant _current;

        public TestClock(Instant current)
        {
            _current = current;
        }

        public Instant GetCurrentInstant() => _current;

        public void Advance(Duration duration)
        {
            _current = _current.Plus(duration);
        }
    }
}
