using GE.BandSite.Database;
using GE.BandSite.Server.Features.Contact;
using GE.BandSite.Testing.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NodaTime;

namespace GE.BandSite.Server.Tests.Contact;

[TestFixture]
[NonParallelizable]
public class ContactSubmissionServiceTests
{
    private TestPostgresProvider _postgres = null!;
    private GeBandSiteDbContext _db = null!;
    private TestNotifier _notifier = null!;
    private ContactSubmissionService _service = null!;

    [SetUp]
    public async Task SetUp()
    {
        _postgres = new TestPostgresProvider();
        await _postgres.InitializeAsync();

        _db = _postgres.CreateDbContext<GeBandSiteDbContext>();
        await _db.Database.EnsureCreatedAsync();

        _notifier = new TestNotifier();
        _service = new ContactSubmissionService(
            _db,
            _notifier,
            SystemClock.Instance,
            NullLogger<ContactSubmissionService>.Instance);
    }

    [TearDown]
    public async Task TearDown()
    {
        if (_db != null)
        {
            await _db.DisposeAsync();
            _db = null!;
        }

        if (_postgres != null)
        {
            await _postgres.DisposeAsync();
            _postgres = null!;
        }
    }

    [Test]
    public async Task SubmitAsync_WithValidRequest_PersistsAndNotifies()
    {
        var localDate = LocalDate.FromDateTime(DateTime.UtcNow.Date.AddDays(30));
        var request = new ContactSubmissionRequest(
            "Jordan Hart",
            "jordan@example.com",
            "+13125550191",
            "Corporate Event",
            localDate,
            "Chicago, IL",
            "10-Piece",
            "$40k+",
            "Need horn feature for award reveal.");

        var result = await _service.SubmitAsync(request);

        Assert.Multiple(async () =>
        {
            Assert.That(result.Success, Is.True);
            Assert.That(result.SubmissionId, Is.Not.Null);
            Assert.That(_notifier.Notifications, Has.Count.EqualTo(1));

            var stored = await _db.ContactSubmissions.SingleAsync();
            Assert.That(stored.OrganizerEmail, Is.EqualTo("jordan@example.com"));
            Assert.That(stored.EventType, Is.EqualTo("Corporate Event"));
            Assert.That(stored.EventDate, Is.EqualTo(localDate));
        });
    }

    [Test]
    public async Task SubmitAsync_WithMissingEmail_ReturnsErrors()
    {
        var request = new ContactSubmissionRequest(
            "Jordan Hart",
            string.Empty,
            null,
            "Wedding",
            null,
            null,
            "Solo / Duo",
            "Under $10k",
            null);

        var result = await _service.SubmitAsync(request);

        Assert.Multiple(() =>
        {
            Assert.That(result.Success, Is.False);
            Assert.That(result.Errors, Is.Not.Empty);
            Assert.That(_db.ContactSubmissions.Count(), Is.EqualTo(0));
        });
    }

    [Test]
    public async Task DeleteAsync_RemovesSubmission()
    {
        var request = new ContactSubmissionRequest(
            "Jordan Hart",
            "jordan@example.com",
            null,
            "Private Function",
            null,
            "Austin, TX",
            "5-Piece",
            "$10k - $20k",
            null);

        var submitResult = await _service.SubmitAsync(request);
        var deleted = await _service.DeleteAsync(submitResult.SubmissionId!.Value);

        Assert.Multiple(() =>
        {
            Assert.That(deleted, Is.True);
            Assert.That(_db.ContactSubmissions.Count(), Is.EqualTo(0));
        });
    }

    private sealed class TestNotifier : IContactSubmissionNotifier
    {
        public List<ContactSubmissionNotification> Notifications { get; } = new();

        public Task NotifyAsync(ContactSubmissionNotification notification, CancellationToken cancellationToken = default)
        {
            Notifications.Add(notification);
            return Task.CompletedTask;
        }
    }
}
