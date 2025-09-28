using System;
using System.IO;
using GE.BandSite.Database;
using GE.BandSite.Database.Media;
using GE.BandSite.Server.Configuration;
using GE.BandSite.Server.Features.Media.Admin;
using GE.BandSite.Server.Features.Media.Processing;
using GE.BandSite.Server.Features.Media.Storage;
using GE.BandSite.Testing.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NodaTime;

namespace GE.BandSite.Server.Tests.Integration;

[TestFixture]
[NonParallelizable]
public class MediaUploadProcessingIntegrationTests
{
    private TestPostgresProvider _postgres = null!;
    private GeBandSiteDbContext _dbContext = null!;
    private LocalMediaStorageDouble _storage = null!;
    private string _root = null!;

    [SetUp]
    public async Task SetUp()
    {
        _postgres = new TestPostgresProvider();
        await _postgres.InitializeAsync();

        _dbContext = _postgres.CreateDbContext<GeBandSiteDbContext>();
        await _dbContext.Database.EnsureCreatedAsync();

        _root = Path.Combine(Path.GetTempPath(), "media-upload-integration", Guid.NewGuid().ToString());
        Directory.CreateDirectory(_root);
        _storage = new LocalMediaStorageDouble(_root);
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

        if (!string.IsNullOrWhiteSpace(_root) && Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Test]
    public async Task VideoUpload_FullPipeline_MarksReadyAndPublishesPlayback()
    {
        var rawVideoKey = SetupRawFile("uploads/raw/videos/highlight.mov", "video-content");
        var rawPosterKey = SetupRawFile("uploads/raw/posters/highlight.jpg", "poster-content");

        var adminService = CreateAdminService();

        var asset = await adminService.CreateVideoAssetAsync(new CreateVideoAssetParameters(
            Title: "Integration Highlight",
            RawVideoKey: rawVideoKey,
            VideoContentType: "video/quicktime",
            Description: "Night one",
            RawPosterKey: rawPosterKey,
            PosterContentType: "image/jpeg",
            IsFeatured: true,
            ShowOnHome: true,
            IsPublished: false,
            DisplayOrder: 1));

        Assert.That(asset.ProcessingState, Is.EqualTo(MediaProcessingState.Pending));

        var processingOptions = Options.Create(new MediaProcessingOptions
        {
            TempDirectory = Path.Combine(_root, "temp"),
            BatchSize = 5
        });

        var storageOptions = Options.Create(new MediaStorageOptions
        {
            VideoPlaybackPrefix = "media/videos/playback"
        });

        var coordinator = new MediaProcessingCoordinator(
            _dbContext,
            new CopyingTranscoder(),
            new ImageSharpImageOptimizer(),
            SystemClock.Instance,
            _storage,
            processingOptions,
            storageOptions,
            NullLogger<MediaProcessingCoordinator>.Instance);

        var processed = await coordinator.ProcessPendingAsync();
        Assert.That(processed, Is.EqualTo(1));

        var stored = await _dbContext.MediaAssets.SingleAsync(x => x.Id == asset.Id);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(stored.ProcessingState, Is.EqualTo(MediaProcessingState.Ready));
            Assert.That(stored.PlaybackPath, Is.Not.Null);
            Assert.That(stored.PlaybackPath, Does.EndWith("_mp4.mp4"));
            Assert.That(stored.DurationSeconds, Is.Not.Null);
            Assert.That(File.Exists(Path.Combine(_root, stored.PlaybackPath!.Replace('/', Path.DirectorySeparatorChar))), Is.True);
        }

        Assert.That(_storage.Uploads, Is.EqualTo(1));
    }

    [Test]
    public async Task RequeueLegacyMov_ReprocessesToMp4()
    {
        var rawVideoKey = SetupRawFile("uploads/raw/videos/legacy.mov", "video-content");
        var adminService = CreateAdminService();

        var asset = await adminService.CreateVideoAssetAsync(new CreateVideoAssetParameters(
            Title: "Legacy Highlight",
            RawVideoKey: rawVideoKey,
            VideoContentType: "video/quicktime",
            Description: null,
            RawPosterKey: null,
            PosterContentType: null,
            IsFeatured: false,
            ShowOnHome: false,
            IsPublished: false,
            DisplayOrder: 5)).ConfigureAwait(false);

        asset.ProcessingState = MediaProcessingState.Ready;
        asset.PlaybackPath = asset.StoragePath;
        asset.LastProcessedAt = SystemClock.Instance.GetCurrentInstant();
        await _dbContext.SaveChangesAsync().ConfigureAwait(false);

        asset.ProcessingState = MediaProcessingState.Pending;
        asset.ProcessingError = null;
        await _dbContext.SaveChangesAsync().ConfigureAwait(false);

        var processingOptions = Options.Create(new MediaProcessingOptions
        {
            TempDirectory = Path.Combine(_root, "temp"),
            BatchSize = 5
        });

        var storageOptions = Options.Create(new MediaStorageOptions
        {
            VideoPlaybackPrefix = "media/videos/playback"
        });

        var coordinator = new MediaProcessingCoordinator(
            _dbContext,
            new CopyingTranscoder(),
            new ImageSharpImageOptimizer(),
            SystemClock.Instance,
            _storage,
            processingOptions,
            storageOptions,
            NullLogger<MediaProcessingCoordinator>.Instance);

        var processed = await coordinator.ProcessPendingAsync().ConfigureAwait(false);
        Assert.That(processed, Is.EqualTo(1));

        var stored = await _dbContext.MediaAssets.SingleAsync(x => x.Id == asset.Id).ConfigureAwait(false);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(stored.ProcessingState, Is.EqualTo(MediaProcessingState.Ready));
            Assert.That(stored.PlaybackPath, Is.Not.Null);
            Assert.That(stored.PlaybackPath, Does.EndWith("_mp4.mp4"));
            Assert.That(File.Exists(Path.Combine(_root, stored.PlaybackPath!.Replace('/', Path.DirectorySeparatorChar))), Is.True);
        }
    }

    private string SetupRawFile(string relativePath, string content)
    {
        var normalized = relativePath.Replace('\\', '/').TrimStart('/');
        var fullPath = Path.Combine(_root, normalized.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, content);
        return normalized;
    }

    private IMediaAdminService CreateAdminService()
    {
        var options = Options.Create(new MediaStorageOptions
        {
            PhotoSourcePrefix = "images/originals",
            PhotoPrefix = "images/optimized",
            VideoSourcePrefix = "videos/originals",
            VideoPlaybackPrefix = "media/videos/playback"
        });

        return new MediaAdminService(
            _dbContext,
            _storage,
            SystemClock.Instance,
            options,
            NullLogger<MediaAdminService>.Instance);
    }

    private sealed class CopyingTranscoder : IMediaTranscoder
    {
        public Task<MediaTranscodeResult> TranscodeAsync(MediaTranscodeRequest request, CancellationToken cancellationToken = default)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(request.OutputPath)!);
            File.Copy(request.InputPath, request.OutputPath, overwrite: true);
            return Task.FromResult(new MediaTranscodeResult(123, 1920, 1080));
        }
    }

    private sealed class LocalMediaStorageDouble : IMediaStorageService
    {
        private readonly string _root;

        public LocalMediaStorageDouble(string root)
        {
            _root = root;
        }

        public int Uploads { get; private set; }

        public Task<PresignedUploadResponse> CreateUploadAsync(MediaUploadRequest request, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task DeleteAsync(string relativePath, CancellationToken cancellationToken = default)
        {
            var path = Resolve(relativePath);
            if (File.Exists(path))
            {
                File.Delete(path);
            }

            return Task.CompletedTask;
        }

        public async Task<string> EnsureLocalCopyAsync(string relativePath, CancellationToken cancellationToken = default)
        {
            var fullPath = Resolve(relativePath);
            if (!File.Exists(fullPath))
            {
                throw new FileNotFoundException(fullPath);
            }

            return await Task.FromResult(fullPath);
        }

        public string NormalizeKey(string key)
        {
            return key.Replace('\\', '/').TrimStart('/');
        }

        public Task<string> PromotePhotoAsync(string rawKey, Guid assetId, string fileName, string contentType, CancellationToken cancellationToken = default)
        {
            var destination = NormalizeKey(Path.Combine("images", "originals", fileName));
            Copy(rawKey, destination);
            return Task.FromResult(destination);
        }

        public Task<string> PromotePosterAsync(string rawKey, Guid assetId, string fileName, string contentType, CancellationToken cancellationToken = default)
        {
            var destination = NormalizeKey(Path.Combine("thumbnails", fileName));
            Copy(rawKey, destination);
            return Task.FromResult(destination);
        }

        public Task<string> PromoteVideoSourceAsync(string rawKey, Guid assetId, string fileName, string contentType, CancellationToken cancellationToken = default)
        {
            var destination = NormalizeKey(Path.Combine("videos", "originals", fileName));
            Copy(rawKey, destination);
            return Task.FromResult(destination);
        }

        public Task UploadFromFileAsync(string relativePath, string filePath, string contentType, CancellationToken cancellationToken = default)
        {
            var destination = Resolve(relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.Copy(filePath, destination, overwrite: true);
            Uploads++;
            return Task.CompletedTask;
        }

        private void Copy(string sourceRelative, string destinationRelative)
        {
            var source = Resolve(sourceRelative);
            var destination = Resolve(destinationRelative);
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.Copy(source, destination, overwrite: true);
        }

        private string Resolve(string relativePath)
        {
            var safe = NormalizeKey(relativePath);
            return Path.Combine(_root, safe.Replace('/', Path.DirectorySeparatorChar));
        }
    }
}
