using System.ComponentModel.DataAnnotations;
using GE.BandSite.Database;
using GE.BandSite.Database.Organization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NodaTime;

namespace GE.BandSite.Server.Features.Contact;

public interface IContactNotificationRecipientProvider
{
    Task<IReadOnlyList<string>> GetRecipientEmailsAsync(CancellationToken cancellationToken = default);
}

public interface IContactNotificationSettingsService : IContactNotificationRecipientProvider
{
    Task<IReadOnlyList<ContactNotificationRecipientModel>> GetRecipientsAsync(CancellationToken cancellationToken = default);

    Task UpdateRecipientsAsync(IEnumerable<string> emails, CancellationToken cancellationToken = default);
}

public sealed class ContactNotificationSettingsService : IContactNotificationSettingsService
{
    private readonly IGeBandSiteDbContext _dbContext;
    private readonly IClock _clock;
    private readonly ILogger<ContactNotificationSettingsService> _logger;

    public ContactNotificationSettingsService(IGeBandSiteDbContext dbContext, IClock clock, ILogger<ContactNotificationSettingsService> logger)
    {
        _dbContext = dbContext;
        _clock = clock;
        _logger = logger;
    }

    public async Task<IReadOnlyList<ContactNotificationRecipientModel>> GetRecipientsAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.ContactNotificationRecipients
            .AsNoTracking()
            .OrderBy(x => x.Email)
            .Select(x => new ContactNotificationRecipientModel(x.Id, x.Email, x.CreatedAt, x.UpdatedAt))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<string>> GetRecipientEmailsAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.ContactNotificationRecipients
            .AsNoTracking()
            .OrderBy(x => x.Email)
            .Select(x => x.Email)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task UpdateRecipientsAsync(IEnumerable<string> emails, CancellationToken cancellationToken = default)
    {
        if (emails is null)
        {
            throw new ArgumentNullException(nameof(emails));
        }

        var normalized = NormalizeEmails(emails);

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        var existing = await _dbContext.ContactNotificationRecipients
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (existing.Count > 0)
        {
            _dbContext.ContactNotificationRecipients.RemoveRange(existing);
        }

        if (normalized.Count > 0)
        {
            var timestamp = _clock.GetCurrentInstant();
            foreach (var email in normalized)
            {
                var entity = new ContactNotificationRecipient
                {
                    Id = Guid.NewGuid(),
                    Email = email,
                    CreatedAt = timestamp,
                    UpdatedAt = timestamp
                };

                _dbContext.ContactNotificationRecipients.Add(entity);
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("Updated contact notification recipients: {RecipientCount} configured.", normalized.Count);
    }

    private static IReadOnlyList<string> NormalizeEmails(IEnumerable<string> emails)
    {
        var validator = new EmailAddressAttribute();
        var result = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var raw in emails)
        {
            var candidate = raw?.Trim();
            if (string.IsNullOrEmpty(candidate))
            {
                continue;
            }

            if (!validator.IsValid(candidate))
            {
                throw new ValidationException($"Invalid email address: {candidate}");
            }

            if (seen.Add(candidate))
            {
                result.Add(candidate);
            }
        }

        return result;
    }
}

public sealed record ContactNotificationRecipientModel(Guid Id, string Email, Instant CreatedAt, Instant? UpdatedAt);
