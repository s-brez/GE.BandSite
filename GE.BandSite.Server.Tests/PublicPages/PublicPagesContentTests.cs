using GE.BandSite.Server.Features.Contact;
using GE.BandSite.Server.Features.Organization;
using GE.BandSite.Server.Features.Organization.Models;
using GE.BandSite.Server.Features.Media;
using GE.BandSite.Server.Features.Media.Models;
using AboutIndexModel = GE.BandSite.Server.Pages.About.IndexModel;
using BandIndexModel = GE.BandSite.Server.Pages.Band.IndexModel;
using ContactIndexModel = GE.BandSite.Server.Pages.Contact.IndexModel;
using EventsIndexModel = GE.BandSite.Server.Pages.Events.IndexModel;
using MediaIndexModel = GE.BandSite.Server.Pages.Media.IndexModel;
using ServicesIndexModel = GE.BandSite.Server.Pages.Services.IndexModel;
using TestimonialsIndexModel = GE.BandSite.Server.Pages.Testimonials.IndexModel;

namespace GE.BandSite.Server.Tests.PublicPages;

[TestFixture]
public class AboutIndexModelTests
{
    [Test]
    public void OnGet_PopulatesStoryAndFacts()
    {
        var model = new AboutIndexModel();

        model.OnGet();

        Assert.Multiple(() =>
        {
            Assert.That(model.HeroTitle, Is.Not.Empty);
            Assert.That(model.StoryParagraphs, Has.Count.GreaterThanOrEqualTo(3));
            Assert.That(model.SpotlightFacts, Has.Count.EqualTo(3));
            Assert.That(model.ShowcaseImages, Has.Count.EqualTo(2));
        });
    }
}

[TestFixture]
public class BandIndexModelTests
{
    [Test]
    public void OnGet_ReturnsTenMembersAndConfigurations()
    {
        var model = new BandIndexModel(new StubOrganizationContentService());

        model.OnGetAsync().GetAwaiter().GetResult();

        Assert.Multiple(() =>
        {
            Assert.That(model.BandMembers, Has.Count.EqualTo(1));
            Assert.That(model.Configurations, Has.Count.EqualTo(3));
            Assert.That(model.TouringHighlights, Is.Not.Empty);
        });
    }
}

[TestFixture]
public class ServicesIndexModelTests
{
    [Test]
    public void OnGet_ExposesServiceAndLineupPackages()
    {
        var model = new ServicesIndexModel();

        model.OnGet();

        Assert.Multiple(() =>
        {
            Assert.That(model.ServicePackages, Has.Count.EqualTo(3));
            Assert.That(model.LineupPackages, Has.Count.EqualTo(3));
            Assert.That(model.AddOns, Is.Not.Empty);
        });
    }
}

[TestFixture]
public class MediaIndexModelTests
{
    [Test]
    public void OnGet_ProvidesFeaturedVideoAndGallery()
    {
        var model = new MediaIndexModel(new StubMediaQueryService());

        model.OnGetAsync().GetAwaiter().GetResult();

        Assert.Multiple(() =>
        {
            Assert.That(model.FeaturedVideo, Is.Not.Null);
            Assert.That(model.PhotoGallery, Has.Count.EqualTo(2));
            Assert.That(model.VideoGallery, Has.Count.EqualTo(2));
        });
    }
}

[TestFixture]
public class TestimonialsIndexModelTests
{
    [Test]
    public void OnGet_ReturnsThreeTestimonials()
    {
        var model = new TestimonialsIndexModel(new StubOrganizationContentService());

        model.OnGetAsync().GetAwaiter().GetResult();

        Assert.That(model.Testimonials, Has.Count.EqualTo(3));
    }
}

[TestFixture]
public class EventsIndexModelTests
{
    [Test]
    public void OnGet_ListsUpcomingEvents()
    {
        var model = new EventsIndexModel(new StubOrganizationContentService());

        model.OnGetAsync().GetAwaiter().GetResult();

        Assert.Multiple(() =>
        {
            Assert.That(model.UpcomingEvents, Is.Not.Empty);
            Assert.That(model.BookingNotes, Is.Not.Empty);
        });
    }
}

[TestFixture]
public class ContactIndexModelTests
{
    [Test]
    public void OnGet_PopulatesManagerAndFormOptions()
    {
        var model = new ContactIndexModel(new FakeContactSubmissionService());

        model.OnGet();

        Assert.Multiple(() =>
        {
            Assert.That(model.Manager.Email, Is.EqualTo("bookings@swingtheboogie.com"));
            Assert.That(model.FormDefinition.EventTypes, Has.Count.GreaterThanOrEqualTo(4));
            Assert.That(model.FaqEntries, Has.Count.EqualTo(3));
        });
    }
}

internal sealed class FakeContactSubmissionService : IContactSubmissionService
{
    public Task<ContactSubmissionResult> SubmitAsync(ContactSubmissionRequest request, CancellationToken cancellationToken = default) =>
        Task.FromResult(ContactSubmissionResult.FromSuccess(Guid.NewGuid()));

    public Task<IReadOnlyList<ContactSubmissionListItem>> GetRecentAsync(int take, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<ContactSubmissionListItem>>(Array.Empty<ContactSubmissionListItem>());

    public Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(true);
}

internal sealed class StubMediaQueryService : IMediaQueryService
{
    public Task<HomeMediaModel> GetHomeHighlightsAsync(CancellationToken cancellationToken = default)
    {
        var video = new MediaItem(Guid.NewGuid(), "Highlight", "Energy clip", "https://cdn.example.com/highlight.mp4", "https://cdn.example.com/highlight.jpg", Array.Empty<string>(), "Video");
        var photos = new List<MediaItem>
        {
            new(Guid.NewGuid(), "Photo A", null, "https://cdn.example.com/photoA.jpg", null, Array.Empty<string>(), "Photo"),
            new(Guid.NewGuid(), "Photo B", null, "https://cdn.example.com/photoB.jpg", null, Array.Empty<string>(), "Photo")
        };
        return Task.FromResult(new HomeMediaModel(video, photos));
    }

    public Task<MediaGalleryModel> GetGalleryAsync(CancellationToken cancellationToken = default)
    {
        var videos = new List<MediaItem>
        {
            new(Guid.NewGuid(), "Video A", "Festival drop", "https://cdn.example.com/videoA.mp4", "https://cdn.example.com/posterA.jpg", Array.Empty<string>(), "Video"),
            new(Guid.NewGuid(), "Video B", "Cocktail trio", "https://cdn.example.com/videoB.mp4", "https://cdn.example.com/posterB.jpg", Array.Empty<string>(), "Video")
        };
        var photos = new List<MediaItem>
        {
            new(Guid.NewGuid(), "Photo C", null, "https://cdn.example.com/photoC.jpg", null, Array.Empty<string>(), "Photo"),
            new(Guid.NewGuid(), "Photo D", null, "https://cdn.example.com/photoD.jpg", null, Array.Empty<string>(), "Photo")
        };
        return Task.FromResult(new MediaGalleryModel(videos, photos));
    }
}

internal sealed class StubOrganizationContentService : IOrganizationContentService
{
    public Task<IReadOnlyList<BandMemberModel>> GetActiveBandMembersAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<BandMemberModel> members = new List<BandMemberModel>
        {
            new("Member", "Role", "Spotlight")
        };

        return Task.FromResult(members);
    }

    public Task<IReadOnlyList<TestimonialModel>> GetPublishedTestimonialsAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<TestimonialModel> testimonials = new List<TestimonialModel>
        {
            new("Quote 1", "Name 1", "Role 1"),
            new("Quote 2", "Name 2", "Role 2"),
            new("Quote 3", "Name 3", "Role 3")
        };

        return Task.FromResult(testimonials);
    }

    public Task<IReadOnlyList<EventListingModel>> GetPublishedEventsAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<EventListingModel> events = new List<EventListingModel>
        {
            new("Event", new NodaTime.LocalDate(2025, 7, 12), "Chicago", "Description")
        };

        return Task.FromResult(events);
    }
}
