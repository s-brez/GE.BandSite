using GE.BandSite.Database;
using GE.BandSite.Database.Organization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NodaTime;

namespace GE.BandSite.Server.Features.Contact;

public interface IContactSubmissionService
{
    Task<ContactSubmissionResult> SubmitAsync(ContactSubmissionRequest request, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ContactSubmissionListItem>> GetRecentAsync(int take, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}

public sealed class ContactSubmissionService : IContactSubmissionService
{
    private readonly IGeBandSiteDbContext _dbContext;
    private readonly IContactSubmissionNotifier _notifier;
    private readonly IClock _clock;
    private readonly ILogger<ContactSubmissionService> _logger;

    public ContactSubmissionService(
        IGeBandSiteDbContext dbContext,
        IContactSubmissionNotifier notifier,
        IClock clock,
        ILogger<ContactSubmissionService> logger)
    {
        _dbContext = dbContext;
        _notifier = notifier;
        _clock = clock;
        _logger = logger;
    }

    public async Task<ContactSubmissionResult> SubmitAsync(ContactSubmissionRequest request, CancellationToken cancellationToken = default)
    {
        var errors = Validate(request);
        if (errors.Count > 0)
        {
            return ContactSubmissionResult.Failed(errors);
        }

        var entity = new ContactSubmission
        {
            Id = Guid.NewGuid(),
            OrganizerName = request.OrganizerName.Trim(),
            OrganizerEmail = request.OrganizerEmail.Trim(),
            OrganizerPhone = request.OrganizerPhone?.Trim(),
            EventType = request.EventType.Trim(),
            EventDate = request.EventDate,
            EventTimezone = string.IsNullOrWhiteSpace(request.EventTimezone) ? null : request.EventTimezone.Trim(),
            Location = request.Location?.Trim(),
            PreferredBandSize = request.PreferredBandSize.Trim(),
            BudgetRange = request.BudgetRange.Trim(),
            Message = string.IsNullOrWhiteSpace(request.Message) ? null : request.Message.Trim(),
            CreatedAt = _clock.GetCurrentInstant()
        };

        await _dbContext.ContactSubmissions.AddAsync(entity, cancellationToken).ConfigureAwait(false);
        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            await _notifier.NotifyAsync(new ContactSubmissionNotification(
                entity.Id,
                entity.OrganizerName,
                entity.OrganizerEmail,
                entity.OrganizerPhone,
                entity.EventType,
                entity.EventDate,
                entity.EventTimezone,
                entity.Location,
                entity.PreferredBandSize,
                entity.BudgetRange,
                entity.Message,
                entity.CreatedAt),
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Contact submission {SubmissionId} saved but notification failed.", entity.Id);
        }

        return ContactSubmissionResult.FromSuccess(entity.Id);
    }

    public async Task<IReadOnlyList<ContactSubmissionListItem>> GetRecentAsync(int take, CancellationToken cancellationToken = default)
    {
        take = Math.Clamp(take, 1, 200);

        return await _dbContext.ContactSubmissions
            .OrderByDescending(x => x.CreatedAt)
            .Take(take)
            .Select(x => new ContactSubmissionListItem(
                x.Id,
                x.CreatedAt,
                x.OrganizerName,
                x.OrganizerEmail,
                x.OrganizerPhone,
                x.EventType,
                x.EventDate,
                x.EventTimezone,
                x.Location,
                x.PreferredBandSize,
                x.BudgetRange,
                x.Message))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.ContactSubmissions
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            .ConfigureAwait(false);

        if (entity is null)
        {
            return false;
        }

        _dbContext.ContactSubmissions.Remove(entity);
        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }

    private static List<string> Validate(ContactSubmissionRequest request)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(request.OrganizerName))
        {
            errors.Add("Organizer name is required.");
        }

        if (string.IsNullOrWhiteSpace(request.OrganizerEmail))
        {
            errors.Add("Organizer email is required.");
        }
        else if (!request.OrganizerEmail.Contains('@', StringComparison.Ordinal))
        {
            errors.Add("Organizer email must be valid.");
        }

        if (string.IsNullOrWhiteSpace(request.EventType))
        {
            errors.Add("Event type is required.");
        }

        if (string.IsNullOrWhiteSpace(request.PreferredBandSize))
        {
            errors.Add("Preferred band size is required.");
        }

        if (string.IsNullOrWhiteSpace(request.BudgetRange))
        {
            errors.Add("Budget range is required.");
        }

        if (request.Message is { Length: > 2000 })
        {
            errors.Add("Message cannot exceed 2000 characters.");
        }

        if (request.EventDate is { } eventDate && eventDate < LocalDate.FromDateTime(DateTime.UtcNow.Date))
        {
            errors.Add("Event date cannot be in the past.");
        }

        return errors;
    }
}

public sealed record ContactSubmissionRequest(
    string OrganizerName,
    string OrganizerEmail,
    string? OrganizerPhone,
    string EventType,
    LocalDate? EventDate,
    string? EventTimezone,
    string? Location,
    string PreferredBandSize,
    string BudgetRange,
    string? Message);

public sealed record ContactSubmissionResult(bool Success, Guid? SubmissionId, IReadOnlyList<string> Errors)
{
    public static ContactSubmissionResult FromSuccess(Guid submissionId) => new(true, submissionId, Array.Empty<string>());

    public static ContactSubmissionResult Failed(IReadOnlyList<string> errors) => new(false, null, errors);
}

public sealed record ContactSubmissionListItem(
    Guid SubmissionId,
    Instant CreatedAt,
    string OrganizerName,
    string OrganizerEmail,
    string? OrganizerPhone,
    string EventType,
    LocalDate? EventDate,
    string? EventTimezone,
    string? Location,
    string PreferredBandSize,
    string BudgetRange,
    string? Message);

public interface IContactSubmissionNotifier
{
    Task NotifyAsync(ContactSubmissionNotification notification, CancellationToken cancellationToken = default);
}

public sealed record ContactSubmissionNotification(
    Guid SubmissionId,
    string OrganizerName,
    string OrganizerEmail,
    string? OrganizerPhone,
    string EventType,
    LocalDate? EventDate,
    string? EventTimezone,
    string? Location,
    string PreferredBandSize,
    string BudgetRange,
    string? Message,
    Instant CreatedAt);
