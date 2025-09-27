using GE.BandSite.Database;
using GE.BandSite.Database.Media;
using GE.BandSite.Server.Configuration;
using GE.BandSite.Server.Features.Media.Admin;
using GE.BandSite.Server.Features.Media.Storage;
using GE.BandSite.Testing.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NodaTime;

namespace GE.BandSite.Server.Tests.Media;

[TestFixture]
[NonParallelizable]
public class MediaAdminServiceTests
{
    private TestPostgresProvider _postgres = null!;
    private GeBandSiteDbContext _dbContext = null!;
    private RecordingStorageService _storage = null!;
    private IClock _clock = null!;

    [SetUp]
    public async Task SetUp()
    {
        _postgres = new TestPostgresProvider();
        await _postgres.InitializeAsync();

        _dbContext = _postgres.CreateDbContext<GeBandSiteDbContext>();
        await _dbContext.Database.EnsureCreatedAsync();

        _storage = new RecordingStorageService();
        _clock = SystemClock.Instance;
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
    public async Task CreatePhotoAssetAsync_PromotesAndPersists()
    {
        var service = CreateService();

        var parameters = new CreatePhotoAssetParameters(
            Title: "Corporate swing photo",
            RawObjectKey: "uploads/raw/photos/raw-file.jpg",
            ContentType: "image/jpeg",
            Description: "Live at the gala",
            IsFeatured: true,
            ShowOnHome: true,
            IsPublished: true,
            DisplayOrder: 3);

        var asset = await service.CreatePhotoAssetAsync(parameters);

        Assert.Multiple(() =>
        {
            Assert.That(asset.AssetType, Is.EqualTo(MediaAssetType.Photo));
            Assert.That(asset.ProcessingState, Is.EqualTo(MediaProcessingState.Ready));
            Assert.That(asset.StoragePath, Is.EqualTo(_storage.LastPromotedPhotoPath));
            Assert.That(asset.SourcePath, Is.EqualTo(_storage.LastPromotedPhotoPath));
            Assert.That(asset.PosterPath, Is.Null);
        });

        var stored = await _dbContext.MediaAssets.SingleAsync(x => x.Id == asset.Id);
        Assert.That(stored.IsPublished, Is.True);
        Assert.That(_storage.PhotoPromotions.Count, Is.EqualTo(1));
    }

    [Test]
    public async Task CreateVideoAssetAsync_WithPoster_QueuesProcessing()
    {
        var service = CreateService();

        var parameters = new CreateVideoAssetParameters(
            Title: "Highlight reel",
            RawVideoKey: "uploads/raw/videos/highlight.mov",
            VideoContentType: "video/quicktime",
            Description: "Mainstage performance",
            RawPosterKey: "uploads/raw/posters/highlight.jpg",
            PosterContentType: "image/jpeg",
            IsFeatured: false,
            ShowOnHome: false,
            IsPublished: false,
            DisplayOrder: 10);

        var asset = await service.CreateVideoAssetAsync(parameters);

        Assert.Multiple(() =>
        {
            Assert.That(asset.AssetType, Is.EqualTo(MediaAssetType.Video));
            Assert.That(asset.ProcessingState, Is.EqualTo(MediaProcessingState.Pending));
            Assert.That(asset.StoragePath, Is.EqualTo(_storage.LastPromotedVideoPath));
            Assert.That(asset.PlaybackPath, Does.EndWith($"{asset.Id:N}.mp4"));
            Assert.That(asset.PosterPath, Is.EqualTo(_storage.LastPromotedPosterPath));
        });

        Assert.That(_storage.VideoPromotions.Count, Is.EqualTo(1));
        Assert.That(_storage.PosterPromotions.Count, Is.EqualTo(1));
    }

    private MediaAdminService CreateService()
    {
        var storageOptions = Options.Create(new MediaStorageOptions
        {
            VideoPlaybackPrefix = "media/videos/playback"
        });

        return new MediaAdminService(
            _dbContext,
            _storage,
            _clock,
            storageOptions,
            NullLogger<MediaAdminService>.Instance);
    }

    private sealed class RecordingStorageService : IMediaStorageService
    {
        public List<(string Raw, Guid AssetId, string FileName, string ContentType)> PhotoPromotions { get; } = new();
        public List<(string Raw, Guid AssetId, string FileName, string ContentType)> VideoPromotions { get; } = new();
        public List<(string Raw, Guid AssetId, string FileName, string ContentType)> PosterPromotions { get; } = new();

        public string? LastPromotedPhotoPath { get; private set; }
        public string? LastPromotedVideoPath { get; private set; }
        public string? LastPromotedPosterPath { get; private set; }

        public Task<PresignedUploadResponse> CreateUploadAsync(MediaUploadRequest request, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task DeleteAsync(string relativePath, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task<string> EnsureLocalCopyAsync(string relativePath, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(relativePath);
        }

        public string NormalizeKey(string key)
        {
            return key.Replace('\\', '/').TrimStart('/');
        }

        public Task<string> PromotePhotoAsync(string rawKey, Guid assetId, string fileName, string contentType, CancellationToken cancellationToken = default)
        {
            PhotoPromotions.Add((rawKey, assetId, fileName, contentType));
            LastPromotedPhotoPath = $"media/photos/{assetId:N}.jpg";
            return Task.FromResult(LastPromotedPhotoPath);
        }

        public Task<string> PromotePosterAsync(string rawKey, Guid assetId, string fileName, string contentType, CancellationToken cancellationToken = default)
        {
            PosterPromotions.Add((rawKey, assetId, fileName, contentType));
            LastPromotedPosterPath = $"media/posters/{assetId:N}.jpg";
            return Task.FromResult(LastPromotedPosterPath);
        }

        public Task<string> PromoteVideoSourceAsync(string rawKey, Guid assetId, string fileName, string contentType, CancellationToken cancellationToken = default)
        {
            VideoPromotions.Add((rawKey, assetId, fileName, contentType));
            LastPromotedVideoPath = $"media/videos/source/{assetId:N}.mov";
            return Task.FromResult(LastPromotedVideoPath);
        }

        public Task UploadFromFileAsync(string relativePath, string filePath, string contentType, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }
}
