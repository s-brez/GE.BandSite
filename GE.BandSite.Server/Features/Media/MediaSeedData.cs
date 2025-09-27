using GE.BandSite.Database;
using GE.BandSite.Database.Media;
using Microsoft.EntityFrameworkCore;
using NodaTime;

namespace GE.BandSite.Server.Features.Media;

public static class MediaSeedData
{
    public static async Task EnsureSeedDataAsync(GeBandSiteDbContext dbContext, IClock clock, CancellationToken cancellationToken = default)
    {
        if (await dbContext.MediaAssets.AnyAsync(cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        var timestamp = clock.GetCurrentInstant();

        var highlightVideo = new MediaAsset
        {
            Id = Guid.NewGuid(),
            Title = "Swing The Boogie Highlight Reel",
            Description = "A snapshot of the sound, swagger, and crowd energy captured live.",
            AssetType = MediaAssetType.Video,
            StoragePath = "media/highlight-demo.mp4", // Placeholder clip sourced from https://samplelib.com/lib/preview/mp4/sample-5s.mp4
            SourcePath = "media/highlight-demo.mp4",
            PlaybackPath = "media/highlight-demo.mp4",
            PosterPath = "images/media-video-poster.svg",
            ProcessingState = MediaProcessingState.Ready,
            IsFeatured = true,
            ShowOnHome = true,
            IsPublished = true,
            DisplayOrder = 0,
            CreatedAt = timestamp
        };

        var homePhotos = new[]
        {
            new MediaAsset
            {
                Id = Guid.NewGuid(),
                Title = "Corporate gala spotlight",
                Description = "Horn section ignites the dance floor at a Fortune 100 celebration.",
                AssetType = MediaAssetType.Photo,
                StoragePath = "images/media-photo-1.svg",
                PosterPath = null,
                ProcessingState = MediaProcessingState.Ready,
                IsFeatured = true,
                ShowOnHome = true,
                IsPublished = true,
                DisplayOrder = 1,
                CreatedAt = timestamp
            },
            new MediaAsset
            {
                Id = Guid.NewGuid(),
                Title = "Destination wedding first dance",
                Description = "Lakefront vows give way to a vintage swing encore.",
                AssetType = MediaAssetType.Photo,
                StoragePath = "images/media-photo-2.svg",
                PosterPath = null,
                ProcessingState = MediaProcessingState.Ready,
                IsFeatured = false,
                ShowOnHome = true,
                IsPublished = true,
                DisplayOrder = 2,
                CreatedAt = timestamp
            },
            new MediaAsset
            {
                Id = Guid.NewGuid(),
                Title = "Rooftop soirée sax solo",
                Description = "Sunset solo over Singapore&apos;s skyline.",
                AssetType = MediaAssetType.Photo,
                StoragePath = "images/media-photo-3.svg",
                PosterPath = null,
                ProcessingState = MediaProcessingState.Ready,
                IsFeatured = false,
                ShowOnHome = true,
                IsPublished = true,
                DisplayOrder = 3,
                CreatedAt = timestamp
            }
        };

        var videoSnapshots = new[]
        {
            new MediaAsset
            {
                Id = Guid.NewGuid(),
                Title = "Festival horn drop",
                Description = "Seven-piece horn line raising the energy at Nightfall Fest.",
                AssetType = MediaAssetType.Video,
                SourcePath = "media/snapshot-festival.mp4",
                StoragePath = "media/snapshot-festival.mp4",
                PlaybackPath = "media/snapshot-festival.mp4",
                PosterPath = "images/media-video-alt.svg",
                ProcessingState = MediaProcessingState.Ready,
                IsFeatured = false,
                ShowOnHome = false,
                IsPublished = true,
                DisplayOrder = 4,
                CreatedAt = timestamp
            },
            new MediaAsset
            {
                Id = Guid.NewGuid(),
                Title = "Cocktail hour trio",
                Description = "Piano, violin, and upright bass set an intimate tone.",
                AssetType = MediaAssetType.Video,
                SourcePath = "media/snapshot-intimate.mp4",
                StoragePath = "media/snapshot-intimate.mp4",
                PosterPath = "images/media-video-intimate.svg",
                PlaybackPath = "media/snapshot-intimate.mp4",
                ProcessingState = MediaProcessingState.Ready,
                IsFeatured = false,
                ShowOnHome = false,
                IsPublished = true,
                DisplayOrder = 5,
                CreatedAt = timestamp
            }
        };

        await dbContext.MediaAssets.AddRangeAsync(new[] { highlightVideo }.Concat(homePhotos).Concat(videoSnapshots), cancellationToken).ConfigureAwait(false);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
