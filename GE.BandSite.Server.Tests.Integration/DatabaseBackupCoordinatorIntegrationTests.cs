using System.Linq;
using GE.BandSite.Server.Configuration;
using GE.BandSite.Server.Features.Operations.Backups;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NodaTime;

namespace GE.BandSite.Server.Tests.Integration;

[TestFixture]
[NonParallelizable]
public class DatabaseBackupCoordinatorIntegrationTests
{
    private string _workingDirectory = null!;
    private StubBackupProcess _process = null!;
    private InMemoryBackupStorage _storage = null!;
    private IConfigurationRoot _configuration = null!;
    private TestClock _clock = null!;
    private DatabaseBackupCoordinator _coordinator = null!;

    [SetUp]
    public void SetUp()
    {
        _workingDirectory = Path.Combine(Path.GetTempPath(), "db-backup-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_workingDirectory);

        _process = new StubBackupProcess();
        _storage = new InMemoryBackupStorage();
        _clock = new TestClock(Instant.FromUtc(2025, 1, 15, 3, 45));

        var options = Options.Create(new DatabaseBackupOptions
        {
            Enabled = true,
            BucketName = "ge-band-site-backups",
            KeyPrefix = "backups/database",
            PgDumpPath = "pg_dump",
            RetentionDays = 30,
            WorkingDirectory = _workingDirectory
        });

        var configurationBuilder = new ConfigurationBuilder();
        configurationBuilder.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Database"] = "Host=localhost;Database=bandsite;Username=postgres;Password=secret"
        });

        _configuration = configurationBuilder.Build();
        _storage.UtcNow = () => new DateTimeOffset(_clock.GetCurrentInstant().ToDateTimeUtc());

        _storage.Seed(
            "backups/database/2024/12/ge-band-site-20241201-010000.dump",
            new DateTimeOffset(2024, 12, 1, 1, 0, 0, TimeSpan.Zero));

        _storage.Seed(
            "backups/database/2025/01/ge-band-site-20250110-010000.dump",
            new DateTimeOffset(2025, 1, 10, 1, 0, 0, TimeSpan.Zero));

        _coordinator = new DatabaseBackupCoordinator(
            options,
            _process,
            _storage,
            _configuration,
            _clock,
            NullLogger<DatabaseBackupCoordinator>.Instance);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_workingDirectory))
        {
            Directory.Delete(_workingDirectory, recursive: true);
        }
    }

    [Test]
    public async Task ExecuteAsync_RunsBackup_UploadsAndPrunesOldEntries()
    {
        await _coordinator.ExecuteAsync();

        Assert.Multiple(() =>
        {
            Assert.That(_process.Requests.Count, Is.EqualTo(1));
            Assert.That(_storage.Uploads.Count, Is.EqualTo(1));
            Assert.That(_storage.DeletedKeys, Has.One.EqualTo("backups/database/2024/12/ge-band-site-20241201-010000.dump"));
        });

        var upload = _storage.Uploads.Single();
        Assert.That(upload.bucket, Is.EqualTo("ge-band-site-backups"));
        Assert.That(upload.key, Does.StartWith("backups/database/2025/01/ge-band-site-20250115-034500"));
        Assert.That(File.Exists(upload.filePath), Is.False, "Coordinator should delete the local dump after upload.");
    }

    private sealed class StubBackupProcess : IDatabaseBackupProcess
    {
        public List<DatabaseBackupProcessRequest> Requests { get; } = new();

        public Task<string> CreateDumpAsync(DatabaseBackupProcessRequest request, CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            Directory.CreateDirectory(request.OutputDirectory);
            var filePath = Path.Combine(request.OutputDirectory, $"ge-band-site-{request.Timestamp:yyyyMMdd-HHmmss}.dump");
            File.WriteAllText(filePath, "BACKUP");
            return Task.FromResult(filePath);
        }
    }

    private sealed class InMemoryBackupStorage : IDatabaseBackupStorage
    {
        private readonly Dictionary<string, DateTimeOffset> _objects = new(StringComparer.Ordinal);

        public Func<DateTimeOffset> UtcNow { get; set; } = () => DateTimeOffset.UtcNow;

        public List<(string bucket, string key, string filePath)> Uploads { get; } = new();

        public List<string> DeletedKeys { get; } = new();

        public void Seed(string key, DateTimeOffset lastModified)
        {
            _objects[key] = lastModified;
        }

        public Task UploadAsync(string bucketName, string key, string filePath, CancellationToken cancellationToken = default)
        {
            Uploads.Add((bucketName, key, filePath));
            _objects[key] = UtcNow();
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<DatabaseBackupDescriptor>> ListAsync(string bucketName, string keyPrefix, CancellationToken cancellationToken = default)
        {
            var prefix = keyPrefix.TrimEnd('/');
            var matches = _objects
                .Where(pair => pair.Key.StartsWith(prefix, StringComparison.Ordinal))
                .Select(pair => new DatabaseBackupDescriptor(pair.Key, pair.Value))
                .ToList();

            return Task.FromResult<IReadOnlyList<DatabaseBackupDescriptor>>(matches);
        }

        public Task DeleteAsync(string bucketName, string key, CancellationToken cancellationToken = default)
        {
            DeletedKeys.Add(key);
            _objects.Remove(key);
            return Task.CompletedTask;
        }
    }

    private sealed class TestClock : IClock
    {
        private Instant _instant;

        public TestClock(Instant instant)
        {
            _instant = instant;
        }

        public Instant GetCurrentInstant()
        {
            return _instant;
        }
    }
}
