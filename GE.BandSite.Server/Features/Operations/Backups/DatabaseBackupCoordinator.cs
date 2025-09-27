using System.Linq;
using System.Globalization;
using GE.BandSite.Server.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NodaTime;

namespace GE.BandSite.Server.Features.Operations.Backups;

public interface IDatabaseBackupCoordinator
{
    Task ExecuteAsync(CancellationToken cancellationToken = default);
}

public sealed class DatabaseBackupCoordinator : IDatabaseBackupCoordinator
{
    private readonly DatabaseBackupOptions _options;
    private readonly IDatabaseBackupProcess _process;
    private readonly IDatabaseBackupStorage _storage;
    private readonly IConfiguration _configuration;
    private readonly IClock _clock;
    private readonly ILogger<DatabaseBackupCoordinator> _logger;

    public DatabaseBackupCoordinator(
        IOptions<DatabaseBackupOptions> options,
        IDatabaseBackupProcess process,
        IDatabaseBackupStorage storage,
        IConfiguration configuration,
        IClock clock,
        ILogger<DatabaseBackupCoordinator> logger)
    {
        _options = options.Value;
        _process = process;
        _storage = storage;
        _configuration = configuration;
        _clock = clock;
        _logger = logger;
    }

    public async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("Database backup skipped because the feature is disabled.");
            return;
        }

        var connectionString = _configuration.GetConnectionString(_options.ConnectionStringName);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException($"Connection string '{_options.ConnectionStringName}' was not found.");
        }

        var instant = _clock.GetCurrentInstant();
        var timestamp = new DateTimeOffset(instant.ToDateTimeUtc());
        var workDirectory = ResolveWorkingDirectory();

        var processRequest = new DatabaseBackupProcessRequest(
            connectionString,
            _options.PgDumpPath,
            workDirectory,
            timestamp);

        string? dumpPath = null;
        try
        {
            dumpPath = await _process.CreateDumpAsync(processRequest, cancellationToken).ConfigureAwait(false);
            var objectKey = BuildObjectKey(timestamp);
            await _storage.UploadAsync(_options.BucketName, objectKey, dumpPath, cancellationToken).ConfigureAwait(false);
            await EnforceRetentionAsync(timestamp, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            if (!string.IsNullOrWhiteSpace(dumpPath) && File.Exists(dumpPath))
            {
                File.Delete(dumpPath);
            }
        }
    }

    private string ResolveWorkingDirectory()
    {
        if (!string.IsNullOrWhiteSpace(_options.WorkingDirectory))
        {
            return Path.GetFullPath(_options.WorkingDirectory);
        }

        var fallback = Path.Combine(AppContext.BaseDirectory, "App_Data", "Backups");
        Directory.CreateDirectory(fallback);
        return fallback;
    }

    private string BuildObjectKey(DateTimeOffset timestamp)
    {
        var prefix = _options.KeyPrefix.TrimEnd('/');
        var fileName = $"ge-band-site-{timestamp:yyyyMMdd-HHmmss}.dump";
        return string.Create(CultureInfo.InvariantCulture, $"{prefix}/{timestamp:yyyy/MM}/{fileName}");
    }

    private async Task EnforceRetentionAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (_options.RetentionDays < 1)
        {
            return;
        }

        var cutoff = now.AddDays(-_options.RetentionDays);
        var backups = await _storage.ListAsync(_options.BucketName, _options.KeyPrefix, cancellationToken).ConfigureAwait(false);

        foreach (var descriptor in backups.Where(d => d.LastModified < cutoff))
        {
            await _storage.DeleteAsync(_options.BucketName, descriptor.Key, cancellationToken).ConfigureAwait(false);
        }
    }
}
