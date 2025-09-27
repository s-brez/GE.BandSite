using GE.BandSite.Server.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NodaTime;

namespace GE.BandSite.Server.Features.Operations.Backups;

public sealed class DatabaseBackupHostedService : BackgroundService
{
    private readonly IDatabaseBackupCoordinator _coordinator;
    private readonly DatabaseBackupOptions _options;
    private readonly IClock _clock;
    private readonly ILogger<DatabaseBackupHostedService> _logger;

    public DatabaseBackupHostedService(
        IDatabaseBackupCoordinator coordinator,
        IOptions<DatabaseBackupOptions> options,
        IClock clock,
        ILogger<DatabaseBackupHostedService> logger)
    {
        _coordinator = coordinator;
        _options = options.Value;
        _clock = clock;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("Database backup hosted service disabled via configuration.");
            return;
        }

        _logger.LogInformation("Database backup hosted service started. Nightly run scheduled at {RunAtUtc} UTC.", _options.RunAtUtc);

        while (!stoppingToken.IsCancellationRequested)
        {
            var nowInstant = _clock.GetCurrentInstant();
            var now = new DateTimeOffset(nowInstant.ToDateTimeUtc());
            var delay = DatabaseBackupSchedule.CalculateDelay(now, _options.RunAtUtc);

            if (delay > TimeSpan.Zero)
            {
                try
                {
                    await Task.Delay(delay, stoppingToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }

            try
            {
                await _coordinator.ExecuteAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Database backup failed.");
            }
        }
    }
}
