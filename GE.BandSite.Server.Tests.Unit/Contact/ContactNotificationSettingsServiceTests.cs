using System.ComponentModel.DataAnnotations;
using System.Linq;
using GE.BandSite.Database;
using GE.BandSite.Server.Features.Contact;
using GE.BandSite.Testing.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NodaTime;

namespace GE.BandSite.Server.Tests.Contact;

[TestFixture]
[NonParallelizable]
public class ContactNotificationSettingsServiceTests
{
    private TestPostgresProvider _postgres = null!;
    private GeBandSiteDbContext _dbContext = null!;
    private ContactNotificationSettingsService _service = null!;

    [SetUp]
    public async Task SetUp()
    {
        _postgres = new TestPostgresProvider();
        await _postgres.InitializeAsync();

        _dbContext = _postgres.CreateDbContext<GeBandSiteDbContext>();
        await _dbContext.Database.EnsureCreatedAsync();

        _service = new ContactNotificationSettingsService(
            _dbContext,
            SystemClock.Instance,
            NullLogger<ContactNotificationSettingsService>.Instance);
    }

    [TearDown]
    public async Task TearDown()
    {
        if (_dbContext != null)
        {
            await _dbContext.DisposeAsync();
            _dbContext = null!;
        }

        if (_postgres != null)
        {
            await _postgres.DisposeAsync();
            _postgres = null!;
        }
    }

    [Test]
    public async Task UpdateRecipientsAsync_WithValidEmails_PersistsDistinctAddresses()
    {
        await _service.UpdateRecipientsAsync(new[]
        {
            "events@example.com",
            " sales@example.com ",
            "EVENTS@example.com"
        });

        var addresses = await _service.GetRecipientEmailsAsync();

        Assert.That(addresses, Is.EqualTo(new[]
        {
            "events@example.com",
            "sales@example.com"
        }));
    }

    [Test]
    public void UpdateRecipientsAsync_WithInvalidEmail_ThrowsValidationException()
    {
        Assert.ThrowsAsync<ValidationException>(() => _service.UpdateRecipientsAsync(new[]
        {
            "valid@example.com",
            "not-an-email"
        }));
    }

    [Test]
    public async Task GetRecipientsAsync_ReturnsOrderedMetadata()
    {
        await _service.UpdateRecipientsAsync(new[]
        {
            "gamma@example.com",
            "alpha@example.com",
            "beta@example.com"
        });

        var recipients = await _service.GetRecipientsAsync();

        Assert.That(recipients.Select(x => x.Email), Is.EqualTo(new[]
        {
            "alpha@example.com",
            "beta@example.com",
            "gamma@example.com"
        }));
    }
}
