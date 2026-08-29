using Aegis.Application.Abstractions;
using Aegis.Application.Dtos.Geo;
using Aegis.Infrastructure.Data.Entities;
using Aegis.Infrastructure.External.Censys;
using Aegis.Infrastructure.External.HostDiscovery;
using Aegis.Infrastructure.Geo;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Aegis.Infrastructure.Background;

public sealed class RegionalPrefetchService(
    RegionalPrefetchRegistry registry,
    OverpassCountryWarmer overpassWarmer,
    CountryHostPrefetchIngestor hostIngestor,
    DiscoveredHostRepository hostRepository,
    IViewportBroker viewportBroker,
    IServiceScopeFactory scopeFactory,
    IOptions<RegionalPrefetchOptions> options,
    ILogger<RegionalPrefetchService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var configured = options.Value.CountryCodes is { Length: > 0 } codes && codes.Length > 0
            ? codes.Select(c => c.ToUpperInvariant())
            : CountryBoundingBoxCatalog.AllCountryCodes;

        registry.InitializeCountries(configured);
        logger.LogInformation(
            "Regional prefetch started for {Count} countries",
            configured.Count());

        while (!stoppingToken.IsCancellationRequested)
        {
            if (!options.Value.Enabled)
            {
                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken).ConfigureAwait(false);
                continue;
            }

            await SyncActiveCountryAsync(stoppingToken).ConfigureAwait(false);

            var countryCode = registry.DequeueNextCountry();
            if (countryCode is null)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(options.Value.IdleDelayMs), stoppingToken)
                    .ConfigureAwait(false);
                continue;
            }

            try
            {
                await ProcessCountryBatchAsync(countryCode, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Regional prefetch batch failed for {Country}", countryCode);
            }

            var delayMs = viewportBroker.HasActiveViewers
                ? options.Value.BatchDelayMs
                : Math.Max(options.Value.BatchDelayMs, options.Value.IdleDelayMs);
            await Task.Delay(TimeSpan.FromMilliseconds(delayMs), stoppingToken)
                .ConfigureAwait(false);
        }
    }

    private async Task SyncActiveCountryAsync(CancellationToken cancellationToken)
    {
        if (viewportBroker.Last is not { } viewport)
        {
            return;
        }

        var bbox = new BoundingBoxDto(
            viewport.Box.South,
            viewport.Box.West,
            viewport.Box.North,
            viewport.Box.East);

        var context = await ViewportHostGeocoding
            .ResolveAsync(scopeFactory, bbox, cancellationToken)
            .ConfigureAwait(false);

        registry.SetActiveCountry(context?.CountryCode);
    }

    private async Task ProcessCountryBatchAsync(string countryCode, CancellationToken cancellationToken)
    {
        var state = await hostRepository
            .GetOrCreateIngestStateAsync(countryCode, cancellationToken)
            .ConfigureAwait(false);

        var previous = registry.GetStatus(countryCode);
        var phase = ResolvePhase(state, previous.Phase);

        var (tilesProcessed, tilesTotal, overpassDone) = await overpassWarmer
            .WarmNextBatchAsync(countryCode, state, cancellationToken)
            .ConfigureAwait(false);

        var hostsAdded = 0;
        for (var i = 0; i < Math.Clamp(options.Value.HostIngestBatchesPerCycle, 1, 4); i++)
        {
            hostsAdded += await hostIngestor
                .IngestHostsBatchAsync(countryCode, cancellationToken)
                .ConfigureAwait(false);
        }

        var shodanRegions = await hostIngestor
            .WarmShodanRegionsAsync(countryCode, state, cancellationToken)
            .ConfigureAwait(false);

        if (overpassDone && state.ShodanWarmComplete && !state.PrefetchWarmComplete)
        {
            state.PrefetchWarmComplete = true;
            phase = RegionalPrefetchPhase.Warm;
            logger.LogInformation("Regional prefetch warm complete for {Country}", countryCode);
        }
        else if (state.PrefetchWarmComplete)
        {
            phase = RegionalPrefetchPhase.Refreshing;
        }
        else
        {
            phase = RegionalPrefetchPhase.Warming;
        }

        state.LastPrefetchUtc = DateTimeOffset.UtcNow;
        await hostRepository.SaveIngestStateAsync(state, cancellationToken).ConfigureAwait(false);

        var hostCount = await hostRepository.CountByCountryAsync(countryCode, cancellationToken).ConfigureAwait(false);
        var tilesCached = Math.Min(state.OverpassTileIndex, tilesTotal);

        registry.UpdateStatus(new RegionalPrefetchStatusDto(
            countryCode,
            phase,
            tilesCached,
            tilesTotal,
            hostCount,
            state.LastPrefetchUtc));

        registry.NotifyCountryUpdated(countryCode);

        if (tilesProcessed > 0 || hostsAdded > 0 || shodanRegions > 0)
        {
            logger.LogDebug(
                "Prefetch {Country} [{Phase}]: overpass {Tiles}/{Total}, hosts={Hosts}, shodanRegions={Shodan}",
                countryCode,
                phase,
                tilesCached,
                tilesTotal,
                hostCount,
                shodanRegions);
        }
    }

    private static RegionalPrefetchPhase ResolvePhase(
        CountryIngestStateEntity state,
        RegionalPrefetchPhase current) =>
        state.PrefetchWarmComplete
            ? RegionalPrefetchPhase.Refreshing
            : current is RegionalPrefetchPhase.Queued
                ? RegionalPrefetchPhase.Warming
                : current;
}
