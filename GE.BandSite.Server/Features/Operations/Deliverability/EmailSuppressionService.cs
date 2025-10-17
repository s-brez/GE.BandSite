using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GE.BandSite.Database;
using GE.BandSite.Database.Organization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GE.BandSite.Server.Features.Operations.Deliverability;

public sealed class EmailSuppressionService : IEmailSuppressionService
{
    private readonly GeBandSiteDbContext _dbContext;
    private readonly ILogger<EmailSuppressionService> _logger;

    public EmailSuppressionService(GeBandSiteDbContext dbContext, ILogger<EmailSuppressionService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<bool> IsSuppressedAsync(string email, CancellationToken cancellationToken = default)
    {
        var normalized = NormalizeEmail(email);
        if (normalized == null)
        {
            return false;
        }

        return await _dbContext.EmailSuppressions
            .AsNoTracking()
            .AnyAsync(x => x.NormalizedEmail == normalized && x.ReleasedAt == null, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyDictionary<string, EmailSuppression>> GetSuppressionsAsync(IEnumerable<string> emails, CancellationToken cancellationToken = default)
    {
        var normalizedEmails = emails
            .Select(NormalizeEmail)
            .Where(static email => email != null)
            .Cast<string>()
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        if (normalizedEmails.Length == 0)
        {
            return new Dictionary<string, EmailSuppression>(StringComparer.OrdinalIgnoreCase);
        }

        var suppressions = await _dbContext.EmailSuppressions
            .AsNoTracking()
            .Where(x => normalizedEmails.Contains(x.NormalizedEmail) && x.ReleasedAt == null)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return suppressions.ToDictionary(x => x.Email, StringComparer.OrdinalIgnoreCase);
    }

    public async Task ApplySuppressionAsync(EmailSuppressionRequest request, CancellationToken cancellationToken = default)
    {
        var normalized = NormalizeEmail(request.Email);
        if (normalized == null)
        {
            _logger.LogWarning("Suppression not applied because email is invalid or empty.");
            return;
        }

        var suppression = await _dbContext.EmailSuppressions
            .SingleOrDefaultAsync(x => x.NormalizedEmail == normalized, cancellationToken)
            .ConfigureAwait(false);

        if (suppression == null)
        {
            suppression = new EmailSuppression
            {
                Id = Guid.NewGuid(),
                Email = request.Email,
                NormalizedEmail = normalized,
                Reason = request.Reason,
                ReasonDetail = TrimDetail(request.ReasonDetail),
                FirstSuppressedAt = request.SuppressedAt,
                LastSuppressedAt = request.SuppressedAt,
                SuppressionCount = 1,
                FeedbackEventId = request.FeedbackEventId
            };

            _dbContext.EmailSuppressions.Add(suppression);
            _logger.LogInformation("Created suppression for {Email} with reason {Reason}.", request.Email, request.Reason);
            return;
        }

        suppression.Email = request.Email;
        suppression.Reason = request.Reason;
        suppression.ReasonDetail = TrimDetail(request.ReasonDetail);
        suppression.LastSuppressedAt = request.SuppressedAt;
        suppression.FeedbackEventId = request.FeedbackEventId;
        suppression.ReleasedAt = null;
        suppression.ReleaseDetail = null;
        suppression.SuppressionCount = suppression.SuppressionCount <= 0 ? 1 : suppression.SuppressionCount + 1;

        if (suppression.FirstSuppressedAt == default)
        {
            suppression.FirstSuppressedAt = request.SuppressedAt;
        }

        _logger.LogInformation("Updated suppression for {Email} with reason {Reason}.", request.Email, request.Reason);
    }

    private static string? NormalizeEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return null;
        }

        return email.Trim().ToLowerInvariant();
    }

    private static string? TrimDetail(string? detail)
    {
        if (string.IsNullOrWhiteSpace(detail))
        {
            return null;
        }

        var value = detail.Trim();
        return value.Length <= 256 ? value : value.Substring(0, 256);
    }
}
