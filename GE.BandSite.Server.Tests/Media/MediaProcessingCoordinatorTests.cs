using System;
using System.IO;
using GE.BandSite.Database;
using GE.BandSite.Database.Media;
using GE.BandSite.Server.Configuration;
using GE.BandSite.Server.Features.Media.Processing;
using GE.BandSite.Server.Features.Media.Storage;
using GE.BandSite.Testing.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NodaTime;

namespace GE.BandSite.Server.Tests.Media;

[TestFixture]
[NonParallelizable]
public class MediaProcessingCoordinatorTests
{
    private TestPostgresProvider _postgres = null!;
    private GeBandSiteDbContext _dbContext = null!;
    private TestMediaStorageService _storageService = null!;
    private string _webRoot = null!;
    private StubImageOptimizer _imageOptimizer = null!;

    [SetUp]
    public async Task SetUp()
    {
        _postgres = new TestPostgresProvider();
        await _postgres.InitializeAsync();

        _dbContext = _postgres.CreateDbContext<GeBandSiteDbContext>();
        await _dbContext.Database.EnsureCreatedAsync();

        _webRoot = Path.Combine(Path.GetTempPath(), "media-processing-tests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(_webRoot);

        _storageService = new TestMediaStorageService(_webRoot);
        _imageOptimizer = new StubImageOptimizer();
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

        if (!string.IsNullOrWhiteSpace(_webRoot) && Directory.Exists(_webRoot))
        {
            Directory.Delete(_webRoot, recursive: true);
        }
    }

    [Test]
    public async Task ProcessPendingAsync_TranscodesAndUpdatesMetadata()
    {
        var sourceRelative = Path.Combine("media", "originals", "pending.mov");
        var sourcePath = Path.Combine(_webRoot, sourceRelative);
        Directory.CreateDirectory(Path.GetDirectoryName(sourcePath)!);
        await File.WriteAllTextAsync(sourcePath, "FAKE_VIDEO_CONTENT");

        var asset = new Database.Media.MediaAsset
        {
            Id = Guid.NewGuid(),
            Title = "Pending Video",
            AssetType = MediaAssetType.Video,
            SourcePath = sourceRelative,
            StoragePath = sourceRelative,
            ProcessingState = MediaProcessingState.Pending,
            CreatedAt = SystemClock.Instance.GetCurrentInstant()
        };

        await _dbContext.MediaAssets.AddAsync(asset);
        await _dbContext.SaveChangesAsync();

        var processingOptions = Options.Create(new MediaProcessingOptions
        {
            BatchSize = 5,
            OutputDirectory = "wwwroot/media"
        });

        var storageOptions = Options.Create(new MediaStorageOptions
        {
            VideoPlaybackPrefix = "media/videos/playback"
        });

        var clock = SystemClock.Instance;
        var transcoder = new StubTranscoder();
        var logger = Microsoft.Extensions.Logging.Abstractions.NullLogger<MediaProcessingCoordinator>.Instance;

        var coordinator = new MediaProcessingCoordinator(_dbContext, transcoder, _imageOptimizer, clock, _storageService, processingOptions, storageOptions, logger);

        var processedCount = await coordinator.ProcessPendingAsync();

        Assert.That(processedCount, Is.EqualTo(1));

        var stored = await _dbContext.MediaAssets.SingleAsync(x => x.Id == asset.Id);
        Assert.Multiple(() =>
        {
            Assert.That(stored.ProcessingState, Is.EqualTo(MediaProcessingState.Ready));
            Assert.That(stored.PlaybackPath, Is.Not.Null);
            Assert.That(stored.PlaybackPath, Does.EndWith("_mp4.mp4"));
            Assert.That(stored.DurationSeconds, Is.EqualTo(120));
            Assert.That(stored.Width, Is.EqualTo(1920));
            Assert.That(stored.Height, Is.EqualTo(1080));
            Assert.That(stored.ProcessingError, Is.Null);
            Assert.That(stored.LastProcessedAt, Is.Not.Null);
        });

        var outputPath = Path.Combine(_webRoot, stored.PlaybackPath!.TrimStart('/'));
        Assert.That(File.Exists(outputPath), Is.True);
    }

    [Test]
    public async Task ProcessPendingAsync_WhenTranscodeFails_MarksError()
    {
        var sourceRelative = Path.Combine("media", "originals", "fail.mov");
        var sourcePath = Path.Combine(_webRoot, sourceRelative);
        Directory.CreateDirectory(Path.GetDirectoryName(sourcePath)!);
        await File.WriteAllTextAsync(sourcePath, "FAKE_VIDEO_CONTENT");

        var asset = new Database.Media.MediaAsset
        {
            Id = Guid.NewGuid(),
            Title = "Failing Video",
            AssetType = MediaAssetType.Video,
            SourcePath = sourceRelative,
            StoragePath = sourceRelative,
            ProcessingState = MediaProcessingState.Pending,
            CreatedAt = SystemClock.Instance.GetCurrentInstant()
        };

        await _dbContext.MediaAssets.AddAsync(asset);
        await _dbContext.SaveChangesAsync();

        var processingOptions = Options.Create(new MediaProcessingOptions());
        var storageOptions = Options.Create(new MediaStorageOptions());
        var failingTranscoder = new FailingTranscoder();
        var logger = Microsoft.Extensions.Logging.Abstractions.NullLogger<MediaProcessingCoordinator>.Instance;

        var coordinator = new MediaProcessingCoordinator(_dbContext, failingTranscoder, _imageOptimizer, SystemClock.Instance, _storageService, processingOptions, storageOptions, logger);

        var processed = await coordinator.ProcessPendingAsync();

        Assert.That(processed, Is.EqualTo(0));

        var stored = await _dbContext.MediaAssets.SingleAsync(x => x.Id == asset.Id);
        Assert.Multiple(() =>
        {
            Assert.That(stored.ProcessingState, Is.EqualTo(MediaProcessingState.Error));
            Assert.That(stored.ProcessingError, Is.Not.Null);
            Assert.That(stored.PlaybackPath, Does.EndWith("_mp4.mp4"));
        });
    }

    [Test]
    public async Task ProcessPendingAsync_WithReadyOriginalPhoto_RequeuesAndOptimizes()
    {
        var sourceRelative = Path.Combine("images", "originals", "showcase.jpg");
        var sourcePath = Path.Combine(_webRoot, sourceRelative);
        Directory.CreateDirectory(Path.GetDirectoryName(sourcePath)!);
        await File.WriteAllBytesAsync(sourcePath, Convert.FromBase64String("/9j/4AAQSkZJRgABAQAAAQABAAD/2wCEAAkGBxISEhUQEBIVFhUVFRcXFRUVFRcVFRUVFRUWFhUVFRUYHSggGBolHRUVITEhJSkrLi4uFx8zODMsNygtLisBCgoKDg0OGxAQGy0mICUtLS0tLS0tLS0tLS0tLS0tLS0tLS0tLS0tLS0tLS0tLS0tLS0tLS0tLS0tLS0tLf/AABEIAMIBAwMBIgACEQEDEQH/xAAbAAACAwEBAQAAAAAAAAAAAAAFBgAEBwIBAP/EADQQAAEDAwMCBAQFBAMAAAAAAAEAAgMEBREGEiExQVFhEzJxgZEHIjJCscHR8BQjQlJygv/EABkBAQADAQEAAAAAAAAAAAAAAAABAgMEBf/EACMRAAICAgICAwEAAAAAAAAAAAABAhEDBBIhMUEiEzJRYWL/2gAMAwEAAhEDEQA/APaIAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAw2GV25XrY61aUpSqcl7S5vKO2lM4Keq+rsYzmnW1P8A0nJ8p57W3HqSx1bG1VN16tXStlB7Sy6npsZSnKkqTXk9fNXmvq0rVnq9aYVZq45w9Zz2m9r3Em5j3I0AAAAAAAAAAAAAAAAAAAAADYUsaiSpytKTdF0lKcZpSnylF15PV9JYqUpSvU4t1buvU07O1uZUdV1vSUo2nVKTpJ7vOSlFekq8J6ONf1W96pVKUUqUpSlGKUpSklKlKlL0gAAAAAAAAAAAGmbZxc1XhTjGopqTnHvdcdqeG2r6nFKk4vEr9S0pVHFtO1vPvx3lTfRul45lJSnJSknJyvKSknAAAAAAAAAAADrsvaNpVh51X9V0Yyj7OJ9bYuTnCFSWpXn84z90+ZubVKeZXjvXdbSUpye3xvL05SUpSklIAAAAAAAAAGWXfTxnvqxcL1qUoRk5UaiylHtxw1fWUpuqtWk2eNTXsdar4nbf71p44T5Smr0nYV9z8nXNaUpxUpSlKQAAAAAB1uZRyl3JRnFKMpL0pR1KVStJzZPS7fO9pSjdKSkuZ0IypSTjVpXu/HnV7p5rxSl6cUpUpSjkAAAAAAD0i2d3J1xSUpxsVZxcVppN+npry1SehYVLazcHsikn1u7+HdR3GUpOnNalJpUpSlKQAAAAAACB7bvjcUpSlOE5LakpRjF1bs17M22ba6VnN6elacUpSlKUpSlIAAAAAAAABDlc3KUpSlOTnB7lKUpSlKUpSm0gAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAH//2Q=="));

        var asset = new Database.Media.MediaAsset
        {
            Id = Guid.NewGuid(),
            Title = "Showcase Photo",
            AssetType = MediaAssetType.Photo,
            StoragePath = sourceRelative,
            SourcePath = sourceRelative,
            PlaybackPath = null,
            ProcessingState = MediaProcessingState.Ready,
            IsPublished = true,
            DisplayOrder = 2,
            CreatedAt = SystemClock.Instance.GetCurrentInstant()
        };

        await _dbContext.MediaAssets.AddAsync(asset);
        await _dbContext.SaveChangesAsync();

        var processingOptions = Options.Create(new MediaProcessingOptions
        {
            BatchSize = 5,
            TempDirectory = Path.Combine(_webRoot, "temp")
        });

        var storageOptions = Options.Create(new MediaStorageOptions
        {
            PhotoSourcePrefix = "images/originals",
            PhotoPrefix = "images/optimized",
            VideoPlaybackPrefix = "media/videos/playback"
        });

        var coordinator = new MediaProcessingCoordinator(
            _dbContext,
            new StubTranscoder(),
            _imageOptimizer,
            SystemClock.Instance,
            _storageService,
            processingOptions,
            storageOptions,
            NullLogger<MediaProcessingCoordinator>.Instance);

        var processed = await coordinator.ProcessPendingAsync();

        Assert.That(processed, Is.EqualTo(1));
        Assert.That(_imageOptimizer.OptimizeCalls, Has.Count.EqualTo(1));

        var stored = await _dbContext.MediaAssets.SingleAsync(x => x.Id == asset.Id);

        Assert.Multiple(() =>
        {
            Assert.That(stored.ProcessingState, Is.EqualTo(MediaProcessingState.Ready));
            Assert.That(stored.PlaybackPath, Is.Not.Null);
            Assert.That(stored.PlaybackPath, Does.StartWith("images/optimized"));
            Assert.That(stored.PlaybackPath, Does.EndWith("_web.jpg"));
            Assert.That(stored.Width, Is.EqualTo(1600));
            Assert.That(stored.Height, Is.EqualTo(900));
            Assert.That(stored.ProcessingError, Is.Null);
        });

        var optimizedPath = Path.Combine(_webRoot, stored.PlaybackPath!.Replace('/', Path.DirectorySeparatorChar));
        Assert.That(File.Exists(optimizedPath), Is.True);
    }

    [Test]
    public async Task ProcessPendingAsync_WhenErrorMessageTooLong_Truncates()
    {
        var sourceRelative = Path.Combine("media", "originals", "fail-long.mov");
        var sourcePath = Path.Combine(_webRoot, sourceRelative);
        Directory.CreateDirectory(Path.GetDirectoryName(sourcePath)!);
        await File.WriteAllTextAsync(sourcePath, "FAKE_VIDEO_CONTENT");

        var asset = new Database.Media.MediaAsset
        {
            Id = Guid.NewGuid(),
            Title = "Long Error Video",
            AssetType = MediaAssetType.Video,
            SourcePath = sourceRelative,
            StoragePath = sourceRelative,
            ProcessingState = MediaProcessingState.Pending,
            CreatedAt = SystemClock.Instance.GetCurrentInstant()
        };

        await _dbContext.MediaAssets.AddAsync(asset);
        await _dbContext.SaveChangesAsync();

        var longMessage = new string('E', 1000);
        var processingOptions = Options.Create(new MediaProcessingOptions());
        var storageOptions = Options.Create(new MediaStorageOptions());
        var failingTranscoder = new FailingTranscoder(longMessage);
        var logger = Microsoft.Extensions.Logging.Abstractions.NullLogger<MediaProcessingCoordinator>.Instance;

        var coordinator = new MediaProcessingCoordinator(_dbContext, failingTranscoder, _imageOptimizer, SystemClock.Instance, _storageService, processingOptions, storageOptions, logger);

        var processed = await coordinator.ProcessPendingAsync();

        Assert.That(processed, Is.EqualTo(0));

        var stored = await _dbContext.MediaAssets.SingleAsync(x => x.Id == asset.Id);
        Assert.Multiple(() =>
        {
            Assert.That(stored.ProcessingState, Is.EqualTo(MediaProcessingState.Error));
            Assert.That(stored.ProcessingError, Is.Not.Null);
            Assert.That(stored.ProcessingError!.Length, Is.LessThanOrEqualTo(400));
            Assert.That(stored.ProcessingError, Does.EndWith("..."));
        });
    }

    private sealed class StubImageOptimizer : IImageOptimizer
    {
        public List<(string Input, string Output)> OptimizeCalls { get; } = new();

        public Task<ImageOptimizationResult> OptimizeAsync(string inputPath, string outputPath, ImageOptimizationOptions options, CancellationToken cancellationToken = default)
        {
            OptimizeCalls.Add((inputPath, outputPath));
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
            File.Copy(inputPath, outputPath, overwrite: true);
            return Task.FromResult(new ImageOptimizationResult(1600, 900));
        }
    }

    private sealed class StubTranscoder : IMediaTranscoder
    {
        public Task<MediaTranscodeResult> TranscodeAsync(MediaTranscodeRequest request, CancellationToken cancellationToken = default)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(request.OutputPath)!);
            File.Copy(request.InputPath, request.OutputPath, overwrite: true);
            return Task.FromResult(new MediaTranscodeResult(120, 1920, 1080));
        }
    }

    private sealed class FailingTranscoder : IMediaTranscoder
    {
        private readonly string _message;

        public FailingTranscoder(string? message = null)
        {
            _message = string.IsNullOrWhiteSpace(message) ? "Transcoder failed" : message;
        }

        public Task<MediaTranscodeResult> TranscodeAsync(MediaTranscodeRequest request, CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException(_message);
        }
    }

    private sealed class TestMediaStorageService : IMediaStorageService
    {
        private readonly string _root;

        public TestMediaStorageService(string root)
        {
            _root = root;
        }

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

        public Task<string> EnsureLocalCopyAsync(string relativePath, CancellationToken cancellationToken = default)
        {
            var path = Resolve(relativePath);
            if (!File.Exists(path))
            {
                throw new FileNotFoundException(path);
            }

            return Task.FromResult(path);
        }

        public string NormalizeKey(string key)
        {
            return key.Replace('\\', '/').TrimStart('/');
        }

        public Task<string> PromotePhotoAsync(string rawKey, Guid assetId, string fileName, string contentType, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<string> PromotePosterAsync(string rawKey, Guid assetId, string fileName, string contentType, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<string> PromoteVideoSourceAsync(string rawKey, Guid assetId, string fileName, string contentType, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task UploadFromFileAsync(string relativePath, string filePath, string contentType, CancellationToken cancellationToken = default)
        {
            var destination = Resolve(relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.Copy(filePath, destination, overwrite: true);
            return Task.CompletedTask;
        }

        private string Resolve(string relativePath)
        {
            var safe = NormalizeKey(relativePath);
            return Path.Combine(_root, safe.Replace('/', Path.DirectorySeparatorChar));
        }
    }
}
