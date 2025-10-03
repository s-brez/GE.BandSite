using GE.BandSite.Database;
using GE.BandSite.Database.Organization;
using GE.BandSite.Server.Features.Organization;
using GE.BandSite.Testing.Core;
using Microsoft.EntityFrameworkCore;
using NodaTime;

namespace GE.BandSite.Server.Tests.Organization;

[TestFixture]
[NonParallelizable]
public class OrganizationContentServiceTests
{
    private TestPostgresProvider _postgres = null!;
    private GeBandSiteDbContext _dbContext = null!;
    private OrganizationContentService _service = null!;

    [SetUp]
    public async Task SetUp()
    {
        _postgres = new TestPostgresProvider();
        await _postgres.InitializeAsync();

        _dbContext = _postgres.CreateDbContext<GeBandSiteDbContext>();
        await _dbContext.Database.EnsureCreatedAsync();

        _service = new OrganizationContentService(_dbContext);

        var now = SystemClock.Instance.GetCurrentInstant();

        await _dbContext.BandMembers.AddRangeAsync(new[]
        {
            new BandMemberProfile { Id = Guid.NewGuid(), Name = "Active", Role = "Piano", IsActive = true, DisplayOrder = 0, CreatedAt = now },
            new BandMemberProfile { Id = Guid.NewGuid(), Name = "Inactive", Role = "Bass", IsActive = false, DisplayOrder = 1, CreatedAt = now }
        });

        await _dbContext.Testimonials.AddAsync(new Testimonial
        {
            Id = Guid.NewGuid(),
            Quote = "Quote",
            Name = "Name",
            Role = "Role",
            DisplayOrder = 0,
            IsPublished = true,
            CreatedAt = now
        });

        await _dbContext.EventListings.AddAsync(new EventListing
        {
            Id = Guid.NewGuid(),
            Title = "Event",
            EventDate = new LocalDate(2025, 7, 12),
            Location = "Chicago",
            Description = "Description",
            IsPublished = true,
            DisplayOrder = 0,
            CreatedAt = now
        });

        await _dbContext.SaveChangesAsync();
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
    public async Task GetActiveBandMembersAsync_ReturnsOnlyActive()
    {
        var members = await _service.GetActiveBandMembersAsync();

        Assert.Multiple(() =>
        {
            Assert.That(members, Has.Count.EqualTo(1));
            Assert.That(members[0].Name, Is.EqualTo("Active"));
        });
    }

    [Test]
    public async Task GetPublishedTestimonialsAsync_ReturnsPublished()
    {
        var testimonials = await _service.GetPublishedTestimonialsAsync();
        Assert.That(testimonials, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task GetPublishedEventsAsync_ReturnsPublished()
    {
        var eventsList = await _service.GetPublishedEventsAsync();
        Assert.That(eventsList, Has.Count.EqualTo(1));
    }
}
