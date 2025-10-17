using System.Threading.Tasks;
using GE.BandSite.Database;
using GE.BandSite.Server.Features.Operations.Deliverability;
using GE.BandSite.Testing.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Newtonsoft.Json;
using NodaTime;

namespace GE.BandSite.Server.Tests.Integration;

[TestFixture]
[NonParallelizable]
public class SesNotificationProcessorIntegrationTests
{
    private TestPostgresProvider _postgres = null!;
    private GeBandSiteDbContext _dbContext = null!;
    private EmailSuppressionService _suppressionService = null!;
    private SesNotificationProcessor _processor = null!;

    [SetUp]
    public async Task SetUp()
    {
        _postgres = new TestPostgresProvider();
        await _postgres.InitializeAsync();

        _dbContext = _postgres.CreateDbContext<GeBandSiteDbContext>();
        await _dbContext.Database.EnsureCreatedAsync();

        _suppressionService = new EmailSuppressionService(_dbContext, NullLogger<EmailSuppressionService>.Instance);
        _processor = new SesNotificationProcessor(_dbContext, _suppressionService, SystemClock.Instance, NullLogger<SesNotificationProcessor>.Instance);
    }

    [TearDown]
    public async Task TearDown()
    {
        if (_dbContext != null)
        {
            await _dbContext.DisposeAsync();
        }

        if (_postgres != null)
        {
            await _postgres.DisposeAsync();
        }
    }

    [Test]
    public async Task ProcessAsync_PermanentBounce_PersistsEventAndSuppression()
    {
        var notification = BuildBounceNotification("Permanent", "blocked@example.com");

        await _processor.ProcessAsync(BuildEnvelope(), notification, Serialize(notification), CancellationToken.None);

        var storedEvent = await _dbContext.SesFeedbackEvents.Include(x => x.Recipients).SingleAsync();
        var suppression = await _dbContext.EmailSuppressions.SingleAsync();

        Assert.Multiple(() =>
        {
            Assert.That(storedEvent.NotificationType, Is.EqualTo("Bounce"));
            Assert.That(storedEvent.Recipients, Has.Count.EqualTo(1));
            Assert.That(storedEvent.Recipients.Single().BounceType, Is.EqualTo("Permanent"));
            Assert.That(suppression.Email, Is.EqualTo("blocked@example.com"));
            Assert.That(suppression.SuppressionCount, Is.EqualTo(1));
            Assert.That(suppression.ReleasedAt, Is.Null);
        });
    }

    [Test]
    public async Task ProcessAsync_Complaint_PersistsEventAndSuppression()
    {
        var notification = new SesNotificationMessage
        {
            NotificationType = "Complaint",
            Mail = new SesMailObject
            {
                MessageId = "complaint-1",
                Timestamp = "2024-05-05T10:30:00Z",
                Source = "mailer@swingtheboogie.com"
            },
            Complaint = new SesComplaintObject
            {
                ComplaintFeedbackType = "abuse",
                ComplaintSubType = "abusive"
            }
        };

        notification.Complaint!.ComplainedRecipients = new List<SesComplainedRecipient>
        {
            new() { EmailAddress = "complaint@example.com" }
        };

        await _processor.ProcessAsync(BuildEnvelope(), notification, Serialize(notification), CancellationToken.None);

        var storedEvent = await _dbContext.SesFeedbackEvents.Include(x => x.Recipients).SingleAsync();
        var suppression = await _dbContext.EmailSuppressions.SingleAsync();

        Assert.Multiple(() =>
        {
            Assert.That(storedEvent.NotificationType, Is.EqualTo("Complaint"));
            Assert.That(storedEvent.Recipients.Single().ComplaintFeedbackType, Is.EqualTo("abuse"));
            Assert.That(suppression.Email, Is.EqualTo("complaint@example.com"));
            Assert.That(suppression.Reason, Is.EqualTo(EmailSuppressionReason.Complaint));
        });
    }

    [Test]
    public async Task ProcessAsync_TransientBounce_DoesNotSuppress()
    {
        var notification = BuildBounceNotification("Transient", "soft@example.com");

        await _processor.ProcessAsync(BuildEnvelope(), notification, Serialize(notification), CancellationToken.None);

        var storedEvent = await _dbContext.SesFeedbackEvents.Include(x => x.Recipients).SingleAsync();
        var suppressions = await _dbContext.EmailSuppressions.ToListAsync();

        Assert.Multiple(() =>
        {
            Assert.That(storedEvent.NotificationType, Is.EqualTo("Bounce"));
            Assert.That(storedEvent.Recipients.Single().BounceType, Is.EqualTo("Transient"));
            Assert.That(suppressions, Is.Empty);
        });
    }

    [Test]
    public async Task ProcessAsync_BounceTwice_IncrementsSuppressionCount()
    {
        var first = BuildBounceNotification("Permanent", "repeat@example.com");
        var second = BuildBounceNotification("Permanent", "repeat@example.com");

        await _processor.ProcessAsync(BuildEnvelope(), first, Serialize(first), CancellationToken.None);
        await _processor.ProcessAsync(BuildEnvelope(), second, Serialize(second), CancellationToken.None);

        var suppression = await _dbContext.EmailSuppressions.SingleAsync();

        Assert.That(suppression.SuppressionCount, Is.EqualTo(2));
    }

    [Test]
    public async Task DeliverabilityReportService_ReturnsDashboardData()
    {
        var notification = BuildBounceNotification("Permanent", "report@example.com");
        await _processor.ProcessAsync(BuildEnvelope(), notification, Serialize(notification), CancellationToken.None);

        var reportService = new DeliverabilityReportService(_dbContext, SystemClock.Instance, NullLogger<DeliverabilityReportService>.Instance);

        var dashboard = await reportService.GetDashboardAsync(10, 10, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(dashboard.Suppressions, Has.Count.EqualTo(1));
            Assert.That(dashboard.Events, Has.Count.EqualTo(1));
            Assert.That(dashboard.Suppressions[0].Email, Is.EqualTo("report@example.com"));
            Assert.That(dashboard.Events[0].Recipients, Has.Count.EqualTo(1));
        });
    }

    private static SnsMessageEnvelope BuildEnvelope()
    {
        return new SnsMessageEnvelope
        {
            Type = "Notification",
            MessageId = Guid.NewGuid().ToString(),
            TopicArn = "arn:aws:sns:us-east-1:111122223333:ses-bounce"
        };
    }

    private static SesNotificationMessage BuildBounceNotification(string bounceType, string email)
    {
        var notification = new SesNotificationMessage
        {
            NotificationType = "Bounce",
            Mail = new SesMailObject
            {
                MessageId = Guid.NewGuid().ToString(),
                Timestamp = "2024-05-01T12:00:00Z",
                Source = "notifier@swingtheboogie.com"
            },
            Bounce = new SesBounceObject
            {
                BounceType = bounceType,
                BounceSubType = "General",
                FeedbackId = Guid.NewGuid().ToString(),
                Timestamp = "2024-05-01T12:00:00Z"
            }
        };

        notification.Bounce!.BouncedRecipients = new List<SesBouncedRecipient>
        {
            new()
            {
                EmailAddress = email,
                Action = "failed",
                Status = "5.1.1",
                DiagnosticCode = "smtp; 550 5.1.1 user unknown"
            }
        };

        return notification;
    }

    private static string Serialize(SesNotificationMessage message)
    {
        return JsonConvert.SerializeObject(message, Formatting.None);
    }
}
