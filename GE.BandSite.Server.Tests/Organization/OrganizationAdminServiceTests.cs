using GE.BandSite.Database;
using GE.BandSite.Database.Organization;
using GE.BandSite.Server.Features.Organization;
using GE.BandSite.Testing.Core;
using Microsoft.Extensions.Logging.Abstractions;
using NodaTime;

namespace GE.BandSite.Server.Tests.Organization;

[TestFixture]
[NonParallelizable]
public class OrganizationAdminServiceTests
{
    private TestPostgresProvider _postgres = null!;
    private GeBandSiteDbContext _dbContext = null!;
    private OrganizationAdminService _service = null!;

    [SetUp]
    public async Task SetUp()
    {
        _postgres = new TestPostgresProvider();
        await _postgres.InitializeAsync();

        _dbContext = _postgres.CreateDbContext<GeBandSiteDbContext>();
        await _dbContext.Database.EnsureCreatedAsync();

        _service = new OrganizationAdminService(_dbContext, SystemClock.Instance, NullLogger<OrganizationAdminService>.Instance);
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
    public async Task AddOrUpdateBandMemberAsync_InsertsNewRecord()
    {
        var profile = new BandMemberProfile
        {
            Id = Guid.Empty,
            Name = "Test",
            Role = "Guitar",
            Spotlight = "Spotlight",
            DisplayOrder = 2,
            IsActive = true
        };

        await _service.AddOrUpdateBandMemberAsync(profile);

        var stored = await _service.GetBandAsync();
        Assert.That(stored, Has.Count.EqualTo(1));
        Assert.That(stored[0].Name, Is.EqualTo("Test"));
    }

    [Test]
    public async Task AddOrUpdateTestimonialAsync_UpdatesExisting()
    {
        var testimonial = new Testimonial
        {
            Id = Guid.Empty,
            Quote = "Quote",
            Name = "Name",
            DisplayOrder = 0,
            IsPublished = true
        };

        await _service.AddOrUpdateTestimonialAsync(testimonial);

        testimonial.Quote = "Updated";
        await _service.AddOrUpdateTestimonialAsync(testimonial);

        var list = await _service.GetTestimonialsAsync();
        Assert.That(list[0].Quote, Is.EqualTo("Updated"));
    }

    [Test]
    public async Task DeleteEventAsync_RemovesRecord()
    {
        var listing = new EventListing
        {
            Id = Guid.Empty,
            Title = "Event",
            IsPublished = true
        };

        await _service.AddOrUpdateEventAsync(listing);

        var stored = await _service.GetEventsAsync();
        await _service.DeleteEventAsync(stored[0].Id);

        var after = await _service.GetEventsAsync();
        Assert.That(after, Is.Empty);
    }
}
