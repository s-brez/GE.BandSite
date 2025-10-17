using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GE.BandSite.Database;
using GE.BandSite.Database.Organization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using NodaTime;
using NodaTime.Text;

namespace GE.BandSite.Server.Features.Operations.Deliverability;

public sealed class SesNotificationProcessor : ISesNotificationProcessor
{
    private static readonly InstantPattern InstantPattern = InstantPattern.ExtendedIso;

    private readonly GeBandSiteDbContext _dbContext;
    private readonly IEmailSuppressionService _suppressionService;
    private readonly IClock _clock;
    private readonly ILogger<SesNotificationProcessor> _logger;

    public SesNotificationProcessor(
        GeBandSiteDbContext dbContext,
        IEmailSuppressionService suppressionService,
        IClock clock,
        ILogger<SesNotificationProcessor> logger)
    {
        _dbContext = dbContext;
        _suppressionService = suppressionService;
        _clock = clock;
        _logger = logger;
    }

    public async Task ProcessAsync(SnsMessageEnvelope envelope, SesNotificationMessage notification, string rawMessage, CancellationToken cancellationToken = default)
    {
        if (notification.NotificationType == null)
        {
            _logger.LogWarning("SES notification skipped because notificationType is missing.");
            return;
        }

        var notificationType = notification.NotificationType.Trim();
        if (notificationType.Length == 0)
        {
            _logger.LogWarning("SES notification skipped because notificationType is empty.");
            return;
        }

        var mail = notification.Mail;
        var eventId = Guid.NewGuid();
        var receivedAt = ResolveInstant(notification, mail, _clock);

        var eventEntity = new SesFeedbackEvent
        {
            Id = eventId,
            NotificationType = notificationType,
            SesMessageId = mail?.MessageId ?? envelope.MessageId ?? Guid.NewGuid().ToString("N"),
            SesFeedbackId = ResolveFeedbackId(notification),
            ReceivedAt = receivedAt,
            SourceEmail = mail?.Source,
            SourceArn = mail?.SourceArn,
            TopicArn = envelope.TopicArn,
            RawPayload = rawMessage
        };

        var duplicateExists = await _dbContext.SesFeedbackEvents
            .AsNoTracking()
            .AnyAsync(x => x.SesMessageId == eventEntity.SesMessageId, cancellationToken)
            .ConfigureAwait(false);

        if (duplicateExists)
        {
            _logger.LogInformation("Duplicate SES event {MessageId} ignored.", eventEntity.SesMessageId);
            return;
        }

        _dbContext.SesFeedbackEvents.Add(eventEntity);

        switch (notificationType.ToLowerInvariant())
        {
            case "bounce":
                await HandleBounceAsync(eventEntity, notification.Bounce, cancellationToken).ConfigureAwait(false);
                break;
            case "complaint":
                await HandleComplaintAsync(eventEntity, notification.Complaint, cancellationToken).ConfigureAwait(false);
                break;
            case "delivery":
                HandleDelivery(eventEntity, notification.Delivery);
                break;
            default:
                _logger.LogWarning("Received unsupported SES notification type {NotificationType}. Event recorded for auditing.", notificationType);
                break;
        }

        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task HandleBounceAsync(SesFeedbackEvent eventEntity, SesBounceObject? bounce, CancellationToken cancellationToken)
    {
        if (bounce?.BouncedRecipients == null || bounce.BouncedRecipients.Count == 0)
        {
            _logger.LogInformation("Bounce notification {MessageId} did not include recipient data.", eventEntity.SesMessageId);
            return;
        }

        var bounceType = bounce.BounceType ?? string.Empty;
        var suppressedCount = 0;
        var recipients = bounce.BouncedRecipients;

        for (var index = 0; index < recipients.Count; index++)
        {
            var recipient = recipients[index];
            var email = recipient.EmailAddress;
            var normalized = NormalizeEmail(email);
            if (normalized == null)
            {
                continue;
            }

            var recipientEntity = new SesFeedbackRecipient
            {
                Id = Guid.NewGuid(),
                FeedbackEventId = eventEntity.Id,
                Email = email!,
                NormalizedEmail = normalized,
                BounceType = bounceType,
                BounceSubType = bounce.BounceSubType,
                BounceAction = recipient.Action,
                BounceStatus = recipient.Status,
                DiagnosticCode = Truncate(recipient.DiagnosticCode, 512),
                Detail = SerializeDetail(recipient),
                RecipientIndex = index
            };

            eventEntity.Recipients.Add(recipientEntity);

            if (ShouldSuppressBounce(bounceType))
            {
                suppressedCount++;
                var reason = bounceType.Equals("Permanent", StringComparison.OrdinalIgnoreCase)
                    ? EmailSuppressionReason.PermanentBounce
                    : EmailSuppressionReason.UndeterminedBounce;

                var suppressedAt = ParseInstant(bounce.Timestamp) ?? _clock.GetCurrentInstant();
                await _suppressionService.ApplySuppressionAsync(
                    new EmailSuppressionRequest(
                        email!,
                        reason,
                        recipient.Status,
                        eventEntity.Id,
                        suppressedAt),
                    cancellationToken).ConfigureAwait(false);
            }
        }

        if (suppressedCount == 0)
        {
            _logger.LogInformation("Bounce notification {MessageId} stored with {RecipientCount} recipients; no suppression applied.", eventEntity.SesMessageId, recipients.Count);
        }
        else
        {
            _logger.LogWarning("Bounce notification {MessageId} suppressed {Suppressed} of {Total} recipients. BounceType={BounceType} SubType={BounceSubType}.",
                eventEntity.SesMessageId,
                suppressedCount,
                recipients.Count,
                bounceType,
                bounce.BounceSubType);
        }
    }

    private async Task HandleComplaintAsync(SesFeedbackEvent eventEntity, SesComplaintObject? complaint, CancellationToken cancellationToken)
    {
        if (complaint?.ComplainedRecipients == null || complaint.ComplainedRecipients.Count == 0)
        {
            _logger.LogInformation("Complaint notification {MessageId} did not include recipient data.", eventEntity.SesMessageId);
            return;
        }

        var suppressedAt = ParseInstant(complaint.Timestamp) ?? _clock.GetCurrentInstant();
        var recipients = complaint.ComplainedRecipients;

        for (var index = 0; index < recipients.Count; index++)
        {
            var recipient = recipients[index];
            var email = recipient.EmailAddress;
            var normalized = NormalizeEmail(email);
            if (normalized == null)
            {
                continue;
            }

            var recipientEntity = new SesFeedbackRecipient
            {
                Id = Guid.NewGuid(),
                FeedbackEventId = eventEntity.Id,
                Email = email!,
                NormalizedEmail = normalized,
                ComplaintFeedbackType = complaint.ComplaintFeedbackType,
                ComplaintSubType = complaint.ComplaintSubType,
                Detail = SerializeDetail(recipient),
                RecipientIndex = index
            };

            eventEntity.Recipients.Add(recipientEntity);

            await _suppressionService.ApplySuppressionAsync(
                new EmailSuppressionRequest(
                    email!,
                    EmailSuppressionReason.Complaint,
                    complaint.ComplaintFeedbackType,
                    eventEntity.Id,
                    suppressedAt),
                cancellationToken).ConfigureAwait(false);
        }

        _logger.LogWarning("Complaint notification {MessageId} suppressed {RecipientCount} recipients. FeedbackType={ComplaintType}.",
            eventEntity.SesMessageId,
            recipients.Count,
            complaint.ComplaintFeedbackType);
    }

    private void HandleDelivery(SesFeedbackEvent eventEntity, SesDeliveryObject? delivery)
    {
        if (delivery?.Recipients == null || delivery.Recipients.Count == 0)
        {
            return;
        }

        for (var index = 0; index < delivery.Recipients.Count; index++)
        {
            var email = delivery.Recipients[index];
            var normalized = NormalizeEmail(email);
            if (normalized == null)
            {
                continue;
            }

            var recipientEntity = new SesFeedbackRecipient
            {
                Id = Guid.NewGuid(),
                FeedbackEventId = eventEntity.Id,
                Email = email!,
                NormalizedEmail = normalized,
                RecipientIndex = index,
                Detail = SerializeDetail(new { delivery.ProcessingTimeMillis })
            };

            eventEntity.Recipients.Add(recipientEntity);
        }
    }

    private static Instant ResolveInstant(SesNotificationMessage notification, SesMailObject? mail, IClock clock)
    {
        return ParseInstant(notification.Bounce?.Timestamp)
            ?? ParseInstant(notification.Complaint?.Timestamp)
            ?? ParseInstant(notification.Delivery?.Timestamp)
            ?? ParseInstant(mail?.Timestamp)
            ?? clock.GetCurrentInstant();
    }

    private static Instant? ParseInstant(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var result = InstantPattern.Parse(value);
        if (result.Success)
        {
            return result.Value;
        }

        return null;
    }

    private static string? ResolveFeedbackId(SesNotificationMessage notification)
    {
        if (!string.IsNullOrWhiteSpace(notification.Bounce?.FeedbackId))
        {
            return notification.Bounce!.FeedbackId;
        }

        if (!string.IsNullOrWhiteSpace(notification.Complaint?.FeedbackId))
        {
            return notification.Complaint!.FeedbackId;
        }

        return null;
    }

    private static bool ShouldSuppressBounce(string bounceType)
    {
        return bounceType.Equals("Permanent", StringComparison.OrdinalIgnoreCase) ||
               bounceType.Equals("Undetermined", StringComparison.OrdinalIgnoreCase);
    }

    private static string? NormalizeEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return null;
        }

        return email.Trim().ToLowerInvariant();
    }

    private static string? SerializeDetail(object? value)
    {
        if (value == null)
        {
            return null;
        }

        var json = JsonConvert.SerializeObject(value, Formatting.None);
        return Truncate(json, 1024);
    }

    private static string? Truncate(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Length <= maxLength ? value : value.Substring(0, maxLength);
    }
}
