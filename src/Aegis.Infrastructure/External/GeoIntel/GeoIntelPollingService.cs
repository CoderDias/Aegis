using Aegis.Application.Abstractions;
using Aegis.Application.Dtos.Geo;
using Aegis.Infrastructure.External.Weather;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Aegis.Infrastructure.External.GeoIntel;

public sealed class GeoIntelPollingService(
    UsgsEarthquakeClient usgs,
    AisHubClient ais,
    OsmVesselFallbackClient osmVessels,
    WeatherAlertAggregator weatherAlerts,
    IGeoIntelCache cache,
    IViewportBroker viewportBroker,
    IOptions<GeoIntelOptions> options,
    ILogger<GeoIntelPollingService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var pollMinutes = Math.Clamp(options.Value.PollIntervalMinutes, 1, 60);
            var delay = TimeSpan.FromMinutes(pollMinutes);

            if (!options.Value.Enabled || !viewportBroker.HasActiveViewers)
            {
                await Task.Delay(delay, stoppingToken).ConfigureAwait(false);
                continue;
            }

            try
            {
                if (cache.IsSeismicStale(TimeSpan.FromMinutes(options.Value.SeismicCacheMinutes)))
                {
                    var seismic = await usgs.FetchRecentAsync(stoppingToken).ConfigureAwait(false);
                    if (seismic.Count > 0)
                    {
                        cache.SetSeismic(seismic);
                        logger.LogInformation("Cached {Count} seismic events (merged)", seismic.Count);
                    }
                }

                if (cache.IsWeatherAlertsStale(TimeSpan.FromMinutes(options.Value.WeatherAlertsCacheMinutes)))
                {
                    var alerts = await weatherAlerts.FetchActiveAsync(stoppingToken).ConfigureAwait(false);
                    cache.SetWeatherAlerts(alerts);
                    if (alerts.Count > 0)
                    {
                        logger.LogInformation(
                            "Cached {Count} weather alerts (INMET/DWD/JMA/Roshydromet)",
                            alerts.Count);
                    }
                }

                var layers = viewportBroker.ActiveLayers;
                if (layers is not null && viewportBroker.Last is { } viewport)
                {
                    if (!options.Value.UseAisStream &&
                        layers.Ships &&
                        cache.IsShipsStale(
                            BboxKey(viewport),
                            TimeSpan.FromMinutes(options.Value.ShipsCacheMinutes)))
                    {
                        var box = viewport.Box;
                        var bbox = new BoundingBoxDto(box.South, box.West, box.North, box.East);
                        IReadOnlyList<Application.Dtos.Intel.GeoMarkerDto> ships;

                        if (ais.IsConfigured)
                        {
                            ships = await ais.FetchInBboxAsync(bbox, stoppingToken).ConfigureAwait(false);
                        }
                        else if (options.Value.UseOsmShipFallback)
                        {
                            ships = await osmVessels
                                .FetchInBboxAsync(bbox, viewport.Zoom.Value, stoppingToken)
                                .ConfigureAwait(false);
                        }
                        else
                        {
                            ships = [];
                        }

                        if (ships.Count > 0)
                        {
                            cache.SetShips(ships, BboxKey(viewport));
                            logger.LogInformation("Cached {Count} vessel markers ({Source})",
                                ships.Count,
                                options.Value.UseAisStream ? "AISStream" :
                                ais.IsConfigured ? "AIS Hub" : "OSM");
                        }
                    }
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "GeoIntel polling failed");
            }

            var nextDelay = cache.GetSeismic().Count > 0
                ? TimeSpan.FromMinutes(pollMinutes)
                : TimeSpan.FromSeconds(5);
            await Task.Delay(nextDelay, stoppingToken).ConfigureAwait(false);
        }
    }

    private static string BboxKey(Viewport viewport)
    {
        var box = viewport.Box;
        return $"{box.South:F2}:{box.West:F2}:{box.North:F2}:{box.East:F2}";
    }
}
