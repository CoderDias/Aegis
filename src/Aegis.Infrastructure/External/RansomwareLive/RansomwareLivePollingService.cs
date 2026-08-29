using Aegis.Application.Abstractions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Aegis.Infrastructure.External.RansomwareLive;

public sealed class RansomwareLivePollingService(
    RansomwareLiveClient client,
    IRansomwareVictimCache victimCache,
    IViewportBroker viewportBroker,
    ILogger<RansomwareLivePollingService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            if (!viewportBroker.HasActiveViewers)
            {
                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken).ConfigureAwait(false);
                continue;
            }

            var hasCache = victimCache.Get().Count > 0;

            try
            {
                var victims = await client.GetRecentVictimsAsync(stoppingToken).ConfigureAwait(false);
                if (victims.Count > 0)
                {
                    victimCache.Set(victims);
                    logger.LogInformation("Cached {Count} ransomware victims", victims.Count);
                    hasCache = true;
                }
                else if (!hasCache)
                {
                    logger.LogWarning("RansomwareLive returned no geolocated victims");
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "RansomwareLive polling failed");
            }

            var delay = hasCache ? TimeSpan.FromMinutes(30) : TimeSpan.FromSeconds(15);
            await Task.Delay(delay, stoppingToken).ConfigureAwait(false);
        }
    }
}
