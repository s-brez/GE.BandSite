using GE.BandSite.Server.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GE.BandSite.Server.Features.Media.Processing;

public sealed class MediaProcessingHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly MediaProcessingOptions _options;
    private readonly ILogger<MediaProcessingHostedService> _logger;

    public MediaProcessingHostedService(
        IServiceScopeFactory scopeFactory,
        IOptions<MediaProcessingOptions> options,
        ILogger<MediaProcessingHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("Media processing hosted service disabled via configuration.");
            return;
        }

        var delay = TimeSpan.FromSeconds(Math.Max(5, _options.PollIntervalSeconds));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var coordinator = scope.ServiceProvider.GetRequiredService<IMediaProcessingCoordinator>();

                var processed = await coordinator.ProcessPendingAsync(stoppingToken).ConfigureAwait(false);
                if (processed == 0)
                {
                    await Task.Delay(delay, stoppingToken).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException)
            {
                // graceful shutdown
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Unhandled error while processing media queue.");
                await Task.Delay(delay, stoppingToken).ConfigureAwait(false);
            }
        }
    }
}
