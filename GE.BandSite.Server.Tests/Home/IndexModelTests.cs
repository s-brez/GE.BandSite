using GE.BandSite.Server.Features.Media;
using GE.BandSite.Server.Features.Media.Models;
using GE.BandSite.Server.Pages;
using Microsoft.Extensions.Logging.Abstractions;

namespace GE.BandSite.Server.Tests.Home;

[TestFixture]
public class IndexModelTests
{
    [Test]
    public async Task OnGetAsync_AssignsHeroCopyAndCallToAction()
    {
        var mediaService = new StubMediaQueryService();
        var model = new IndexModel(new NullLogger<IndexModel>(), mediaService);

        await model.OnGetAsync();

        Assert.Multiple(() =>
        {
            Assert.That(model.HeroTitle, Is.EqualTo("The world-class swing band bringing the vibe to your event."));
            Assert.That(model.CallToActionText, Is.EqualTo("Book Your Event"));
            Assert.That(model.HighlightVideo, Is.Not.Null);
            Assert.That(model.HighlightPhotos, Has.Count.EqualTo(2));
        });
    }

    private sealed class StubMediaQueryService : IMediaQueryService
    {
        public Task<HomeMediaModel> GetHomeHighlightsAsync(CancellationToken cancellationToken = default)
        {
            var video = new MediaItem(Guid.NewGuid(), "Highlight", "Test highlight", "https://cdn.example.com/video.mp4", "https://cdn.example.com/poster.jpg", Array.Empty<string>(), "Video");
            var photos = new List<MediaItem>
            {
                new(Guid.NewGuid(), "Photo 1", null, "https://cdn.example.com/photo1.jpg", null, Array.Empty<string>(), "Photo"),
                new(Guid.NewGuid(), "Photo 2", null, "https://cdn.example.com/photo2.jpg", null, Array.Empty<string>(), "Photo")
            };

            return Task.FromResult(new HomeMediaModel(video, photos));
        }

        public Task<MediaGalleryModel> GetGalleryAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new MediaGalleryModel(Array.Empty<MediaItem>(), Array.Empty<MediaItem>()));
        }
    }
}
