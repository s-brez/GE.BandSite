using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GE.BandSite.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NodaTime;

namespace GE.BandSite.Server.Features.Operations.Deliverability;

public sealed class DeliverabilityReportService : IDeliverabilityReportService
{
    private readonly GeBandSiteDbContext _dbContext;
    private readonly IClock _clock;
    private readonly ILogger<DeliverabilityReportService> _logger;

    public DeliverabilityReportService(GeBandSiteDbContext dbContext, IClock clock, ILogger<DeliverabilityReportService> logger)
    {
        _dbContext = dbContext;
        _clock = clock;
        _logger = logger;
    }

    public async Task<DeliverabilityDashboardModel> GetDashboardAsync(int suppressionCount, int eventCount, CancellationToken cancellationToken = default)
    {
        suppressionCount = Math.Clamp(suppressionCount, 1, 200);
        eventCount = Math.Clamp(eventCount, 1, 200);

        var suppressionsTask = _dbContext.EmailSuppressions
            .AsNoTracking()
            .Where(x => x.ReleasedAt == null)
            .OrderByDescending(x => x.LastSuppressedAt)
            .Take(suppressionCount)
            .Select(x => new DeliverabilitySuppressionModel(
                x.Id,
                x.Email,
                x.Reason,
                x.ReasonDetail,
                x.SuppressionCount,
                x.FirstSuppressedAt,
                x.LastSuppressedAt,
                x.FeedbackEventId))
            .ToListAsync(cancellationToken);

        var eventsTask = _dbContext.SesFeedbackEvents
            .AsNoTracking()
            .OrderByDescending(x => x.ReceivedAt)
            .Take(eventCount)
            .Select(x => new DeliverabilityEventModel(
                x.Id,
                x.NotificationType,
                x.ReceivedAt,
                x.SesMessageId,
                x.SesFeedbackId,
                x.SourceEmail,
                x.TopicArn,
                x.Recipients
                    .OrderBy(r => r.RecipientIndex)
                    .Select(r => new DeliverabilityEventRecipientModel(
                        r.Email,
                        r.BounceType,
                        r.BounceSubType,
                        r.BounceStatus,
                        r.ComplaintFeedbackType,
                        r.DiagnosticCode,
                        r.Detail))
                    .ToList()))
            .ToListAsync(cancellationToken);

        await Task.WhenAll(suppressionsTask, eventsTask).ConfigureAwait(false);

        return new DeliverabilityDashboardModel(suppressionsTask.Result, eventsTask.Result);
    }

    public async Task<bool> ReleaseSuppressionAsync(Guid suppressionId, string? releaseNote, CancellationToken cancellationToken = default)
    {
        var suppression = await _dbContext.EmailSuppressions
            .SingleOrDefaultAsync(x => x.Id == suppressionId, cancellationToken)
            .ConfigureAwait(false);

        if (suppression == null)
        {
            return false;
        }

        if (suppression.ReleasedAt != null)
        {
            return false;
        }

        suppression.ReleasedAt = _clock.GetCurrentInstant();
        suppression.ReleaseDetail = TrimReleaseNote(releaseNote);

        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("Released suppression for {Email} at {ReleasedAt}.", suppression.Email, suppression.ReleasedAt);
        return true;
    }

    private static string? TrimReleaseNote(string? note)
    {
        if (string.IsNullOrWhiteSpace(note))
        {
            return null;
        }

        var value = note.Trim();
        return value.Length <= 256 ? value : value.Substring(0, 256);
    }
}

public sealed record DeliverabilityDashboardModel(
    IReadOnlyList<DeliverabilitySuppressionModel> Suppressions,
    IReadOnlyList<DeliverabilityEventModel> Events);

public sealed record DeliverabilitySuppressionModel(
    Guid Id,
    string Email,
    string Reason,
    string? ReasonDetail,
    int SuppressionCount,
    Instant FirstSuppressedAt,
    Instant LastSuppressedAt,
    Guid? FeedbackEventId);

public sealed record DeliverabilityEventModel(
    Guid Id,
    string NotificationType,
    Instant ReceivedAt,
    string SesMessageId,
    string? SesFeedbackId,
    string? SourceEmail,
    string? TopicArn,
    IReadOnlyList<DeliverabilityEventRecipientModel> Recipients);

public sealed record DeliverabilityEventRecipientModel(
    string Email,
    string? BounceType,
    string? BounceSubType,
    string? BounceStatus,
    string? ComplaintFeedbackType,
    string? DiagnosticCode,
    string? Detail);
