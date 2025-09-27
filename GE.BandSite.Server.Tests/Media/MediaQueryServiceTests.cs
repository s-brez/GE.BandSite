using GE.BandSite.Database;
using GE.BandSite.Database.Media;
using GE.BandSite.Server.Configuration;
using GE.BandSite.Server.Features.Media;
using GE.BandSite.Server.Features.Media.Models;
using GE.BandSite.Testing.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NodaTime;

namespace GE.BandSite.Server.Tests.Media;

[TestFixture]
[NonParallelizable]
public class MediaQueryServiceTests
{
    private TestPostgresProvider _postgres = null!;
    private GeBandSiteDbContext _dbContext = null!;
    private MediaQueryService _service = null!;

    [SetUp]
    public async Task SetUp()
    {
        _postgres = new TestPostgresProvider();
        await _postgres.InitializeAsync();

        _dbContext = _postgres.CreateDbContext<GeBandSiteDbContext>();
        await _dbContext.Database.EnsureCreatedAsync();

        var options = Options.Create(new MediaDeliveryOptions { BaseUrl = "https://cdn.example.com" });
        _service = new MediaQueryService(_dbContext, options);

        var now = SystemClock.Instance.GetCurrentInstant();

        var assets = new List<Database.Media.MediaAsset>
        {
            new()
            {
                Id = Guid.NewGuid(),
                Title = "Video Home",
                Description = "Highlight",
                StoragePath = "videos/highlight.mp4",
                PlaybackPath = "videos/highlight-processed.mp4",
                PosterPath = "posters/highlight.jpg",
                AssetType = MediaAssetType.Video,
                ProcessingState = MediaProcessingState.Ready,
                IsFeatured = true,
                ShowOnHome = true,
                IsPublished = true,
                DisplayOrder = 0,
                CreatedAt = now
            },
            new()
            {
                Id = Guid.NewGuid(),
                Title = "Photo Home",
                StoragePath = "photos/home.jpg",
                AssetType = MediaAssetType.Photo,
                ShowOnHome = true,
                IsPublished = true,
                DisplayOrder = 1,
                CreatedAt = now
            },
            new()
            {
                Id = Guid.NewGuid(),
                Title = "Gallery Video",
                StoragePath = "videos/gallery.mp4",
                PosterPath = null,
                AssetType = MediaAssetType.Video,
                PlaybackPath = "videos/gallery-processed.mp4",
                ProcessingState = MediaProcessingState.Ready,
                ShowOnHome = false,
                IsPublished = true,
                DisplayOrder = 2,
                CreatedAt = now
            },
            new()
            {
                Id = Guid.NewGuid(),
                Title = "Unpublished",
                StoragePath = "videos/unpublished.mp4",
                AssetType = MediaAssetType.Video,
                ShowOnHome = true,
                ProcessingState = MediaProcessingState.Pending,
                IsPublished = false,
                DisplayOrder = 3,
                CreatedAt = now
            }
        };

        await _dbContext.MediaAssets.AddRangeAsync(assets);
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
    public async Task GetHomeHighlightsAsync_ReturnsFeaturedVideoAndPhotos()
    {
        HomeMediaModel result = await _service.GetHomeHighlightsAsync();

        Assert.Multiple(() =>
        {
            Assert.That(result.FeaturedVideo, Is.Not.Null);
            Assert.That(result.HighlightPhotos, Has.Count.EqualTo(1));
            Assert.That(result.FeaturedVideo!.Url, Is.EqualTo("https://cdn.example.com/videos/highlight-processed.mp4"));
            Assert.That(result.FeaturedVideo!.PosterUrl, Is.EqualTo("https://cdn.example.com/posters/highlight.jpg"));
        });
    }

    [Test]
    public async Task GetGalleryAsync_FiltersByPublicationState()
    {
        MediaGalleryModel result = await _service.GetGalleryAsync();

        Assert.Multiple(() =>
        {
            Assert.That(result.Videos, Has.Count.EqualTo(2));
            Assert.That(result.Photos, Has.Count.EqualTo(1));
            Assert.That(result.Videos.Any(v => v.Title == "Unpublished"), Is.False);
        });
    }
}
