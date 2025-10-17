using System.Security.Cryptography;
using Amazon.SimpleEmailV2.Model;
using GE.BandSite.Database;
using GE.BandSite.Database.Authentication;
using GE.BandSite.Server.Authentication;
using GE.BandSite.Server.Features.Contact; // for ISesEmailClient
using GE.BandSite.Server.Features.Operations.Deliverability;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NodaTime;

namespace GE.BandSite.Server.Services;

public sealed class PasswordResetService : IPasswordResetService
{
    private readonly GeBandSiteDbContext _dbContext;
    private readonly ISesEmailClient _sesEmailClient;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IPasswordValidator _passwordValidator;
    private readonly IEmailSuppressionService _suppressionService;
    private readonly IClock _clock;
    private readonly IOptions<PasswordResetOptions> _options;
    private readonly ILogger<PasswordResetService> _logger;

    public PasswordResetService(
        GeBandSiteDbContext dbContext,
        ISesEmailClient sesEmailClient,
        IPasswordHasher passwordHasher,
        IPasswordValidator passwordValidator,
        IEmailSuppressionService suppressionService,
        IClock clock,
        IOptions<PasswordResetOptions> options,
        ILogger<PasswordResetService> logger)
    {
        _dbContext = dbContext;
        _sesEmailClient = sesEmailClient;
        _passwordHasher = passwordHasher;
        _passwordValidator = passwordValidator;
        _suppressionService = suppressionService;
        _clock = clock;
        _options = options;
        _logger = logger;
    }

    public async Task<PasswordResetRequestResult> RequestPasswordResetAsync(string email, CancellationToken cancellationToken = default)
    {
        string normalizedEmail = email?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(normalizedEmail))
        {
            return new PasswordResetRequestResult { Accepted = true, EmailDispatched = false };
        }

        var options = _options.Value;
        if (!IsDispatchEnabled(options))
        {
            _logger.LogWarning("Password reset request ignored because configuration is disabled or incomplete.");
            return new PasswordResetRequestResult { Accepted = true, EmailDispatched = false };
        }

        var (user, _) = await _dbContext.GetUserByEmailAsync(normalizedEmail, cancellationToken).ConfigureAwait(false);
        if (user == null || !user.IsActive)
        {
            return new PasswordResetRequestResult { Accepted = true, EmailDispatched = false };
        }

        if (await _suppressionService.IsSuppressedAsync(user.Email, cancellationToken).ConfigureAwait(false))
        {
            _logger.LogWarning("Password reset email for {Email} suppressed because address is blocked.", user.Email);
            return new PasswordResetRequestResult { Accepted = true, EmailDispatched = false };
        }

        var now = _clock.GetCurrentInstant();

        var existingRequests = await _dbContext.PasswordResetRequests
            .Where(x => x.UserId == user.Id && x.ConsumedAt == null && x.CancelledAt == null && x.ExpiresAt > now)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (var request in existingRequests)
        {
            request.CancelledAt = now;
        }

        byte[] tokenBytes = RandomNumberGenerator.GetBytes(32);
        string tokenHash = Convert.ToHexString(SHA256.HashData(tokenBytes));
        string token = WebEncoders.Base64UrlEncode(tokenBytes);

        var expiresAt = now.Plus(Duration.FromMinutes(options.GetExpiryMinutes()));
        var passwordResetRequest = new PasswordResetRequest
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = tokenHash,
            CreatedAt = now,
            ExpiresAt = expiresAt
        };

        _dbContext.PasswordResetRequests.Add(passwordResetRequest);
        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        string resetLink = BuildResetLink(options.ResetLinkBaseUrl!, token);
        string subject = string.IsNullOrWhiteSpace(options.Subject)
            ? "Reset your Swing The Boogie admin password"
            : options.Subject!;

        var emailRequest = new SendEmailRequest
        {
            FromEmailAddress = options.FromAddress,
            Destination = new Destination
            {
                ToAddresses = new List<string> { user.Email }
            },
            Content = new EmailContent
            {
                Simple = new Message
                {
                    Subject = new Content { Data = subject },
                    Body = new Body
                    {
                        Html = new Content { Data = BuildHtmlBody(user, resetLink, expiresAt) },
                        Text = new Content { Data = BuildTextBody(user, resetLink, expiresAt) }
                    }
                }
            }
        };

        try
        {
            await _sesEmailClient.SendEmailAsync(emailRequest, cancellationToken).ConfigureAwait(false);
            _logger.LogInformation("Password reset email sent for user {Email}.", user.Email);
            return new PasswordResetRequestResult { Accepted = true, EmailDispatched = true };
        }
        catch (Exception exception)
        {
            passwordResetRequest.CancelledAt = now;
            await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            _logger.LogError(exception, "Failed to send password reset email for user {Email}.", user.Email);
            return new PasswordResetRequestResult { Accepted = true, EmailDispatched = false };
        }
    }

    public async Task<PasswordResetResult> ResetPasswordAsync(string token, string newPassword, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return new PasswordResetResult { Success = false, Error = PasswordResetError.InvalidToken };
        }

        byte[] tokenBytes;
        try
        {
            tokenBytes = WebEncoders.Base64UrlDecode(token);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Failed to decode password reset token.");
            return new PasswordResetResult { Success = false, Error = PasswordResetError.InvalidToken };
        }

        string tokenHash = Convert.ToHexString(SHA256.HashData(tokenBytes));
        var now = _clock.GetCurrentInstant();

        var resetRequest = await _dbContext.PasswordResetRequests
            .Include(x => x.User)
            .SingleOrDefaultAsync(x => x.TokenHash == tokenHash, cancellationToken)
            .ConfigureAwait(false);

        if (resetRequest == null)
        {
            return new PasswordResetResult { Success = false, Error = PasswordResetError.InvalidToken };
        }

        if (resetRequest.CancelledAt != null || resetRequest.ConsumedAt != null)
        {
            return new PasswordResetResult { Success = false, Error = PasswordResetError.AlreadyUsed };
        }

        if (resetRequest.ExpiresAt <= now)
        {
            return new PasswordResetResult { Success = false, Error = PasswordResetError.Expired };
        }

        var user = resetRequest.User;
        if (user == null)
        {
            user = await _dbContext.Users.SingleOrDefaultAsync(x => x.Id == resetRequest.UserId, cancellationToken).ConfigureAwait(false);
            if (user == null)
            {
                return new PasswordResetResult { Success = false, Error = PasswordResetError.InvalidToken };
            }
        }

        if (!user.IsActive)
        {
            return new PasswordResetResult { Success = false, Error = PasswordResetError.InvalidToken };
        }

        var validationResult = _passwordValidator.ValidateWithFeedback(newPassword);
        if (!validationResult.IsValid)
        {
            return new PasswordResetResult
            {
                Success = false,
                Error = PasswordResetError.PasswordValidationFailed,
                PasswordValidationErrors = validationResult.FailedRequirements
            };
        }

        var candidateHash = _passwordHasher.Hash(newPassword, user.Salt);
        if (candidateHash.SequenceEqual(user.PasswordHash))
        {
            return new PasswordResetResult { Success = false, Error = PasswordResetError.PasswordMatchesCurrent };
        }

        if (user.PasswordHash.Length > 0)
        {
            user.PreviousPasswordHashes ??= new List<byte[]>();
            user.PreviousPasswordHashes.Add(user.PasswordHash.ToArray());
        }

        byte[] newSalt = _passwordHasher.GenerateSalt();
        byte[] newHash = _passwordHasher.Hash(newPassword, newSalt);

        user.Salt = newSalt;
        user.PasswordHash = newHash;
        user.PasswordChangeDateTime = now;
        user.IsLocked = false;
        user.LockedDateTime = null;

        resetRequest.ConsumedAt = now;

        var refreshTokens = await _dbContext.RefreshTokens
            .Where(x => x.UserId == user.Id && x.RevokedAt == null)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        foreach (var refreshToken in refreshTokens)
        {
            refreshToken.Revoke("password reset");
        }

        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new PasswordResetResult { Success = true };
    }

    private static bool IsDispatchEnabled(PasswordResetOptions options)
    {
        return options.Enabled
            && !string.IsNullOrWhiteSpace(options.FromAddress)
            && !string.IsNullOrWhiteSpace(options.ResetLinkBaseUrl);
    }

    private static string BuildResetLink(string baseUrl, string token)
    {
        char separator = baseUrl.Contains('?', StringComparison.Ordinal) ? '&' : '?';
        return $"{baseUrl}{separator}token={Uri.EscapeDataString(token)}";
    }

    private static string BuildHtmlBody(User user, string resetLink, Instant expiresAt)
    {
        var expiry = expiresAt.ToDateTimeUtc().ToString("u");
        return $"<p>Hello {System.Net.WebUtility.HtmlEncode(user.FirstName)},</p>" +
               "<p>We received a request to reset your Swing The Boogie admin password. " +
               "Click the button below to choose a new password.</p>" +
               $"<p><a href=\"{System.Net.WebUtility.HtmlEncode(resetLink)}\" " +
               "style=\"display:inline-block;padding:10px 18px;background-color:#b00020;color:#ffffff;text-decoration:none;border-radius:4px;\">Reset password</a></p>" +
               $"<p>This link expires at {expiry} UTC. If you did not request this change, you can safely ignore this email.</p>" +
               "<p>— Swing The Boogie</p>";
    }

    private static string BuildTextBody(User user, string resetLink, Instant expiresAt)
    {
        var expiry = expiresAt.ToDateTimeUtc().ToString("u");
        return $"Hello {user.FirstName},\n\n" +
               "We received a request to reset your Swing The Boogie admin password. " +
               "Open the link below to choose a new password:\n" +
               $"{resetLink}\n\n" +
               $"This link expires at {expiry} UTC. If you did not request this change, you can ignore this email.\n\n" +
               "— Swing The Boogie";
    }
}

public sealed class PasswordResetOptions
{
    public const int DefaultExpiryMinutes = 60;

    public bool Enabled { get; set; } = true;

    public string? FromAddress { get; set; }

    public string? Subject { get; set; }

    public string? ResetLinkBaseUrl { get; set; }

    public int ExpiryMinutes { get; set; } = DefaultExpiryMinutes;

    public int GetExpiryMinutes()
    {
        return ExpiryMinutes > 0 ? ExpiryMinutes : DefaultExpiryMinutes;
    }
}
