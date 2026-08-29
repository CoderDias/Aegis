using Aegis.Application.Geo;
using Aegis.Application.Settings;
using Aegis.Infrastructure.Data.Entities;
using Aegis.Infrastructure.External.Censys;
using Aegis.Infrastructure.External.HostDiscovery;
using Aegis.Infrastructure.External.Shodan;
using Aegis.Infrastructure.Geo;
using Aegis.Infrastructure.Settings;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Aegis.Infrastructure.Background;

public sealed class CountryHostPrefetchIngestor(
    CensysHostDiscoveryClient censysDiscovery,
    FreeHostDiscoveryClient freeDiscovery,
    DiscoveredHostRepository hostRepository,
    ShodanClient shodanClient,
    IntegrationSettingsService integrationSettings,
    IOptions<ShodanOptions> shodanOptions,
    IOptions<CensysOptions> censysOptions,
    IOptions<HostDiscoveryOptions> hostDiscoveryOptions,
    IOptions<RegionalPrefetchOptions> prefetchOptions,
    ILogger<CountryHostPrefetchIngestor> logger)
{
    public async Task<int> IngestHostsBatchAsync(
        string countryCode,
        CancellationToken cancellationToken)
    {
        if (!CountryBoundingBoxCatalog.TryGet(countryCode, out var bbox))
        {
            return 0;
        }

        var before = await hostRepository.CountByCountryAsync(countryCode, cancellationToken).ConfigureAwait(false);

        if (integrationSettings.IsEnabled(IntegrationKeys.Censys) && censysOptions.Value.Enabled)
        {
            await censysDiscovery.IngestCountryBatchAsync(countryCode, cancellationToken).ConfigureAwait(false);
        }
        else if (hostDiscoveryOptions.Value.Enabled &&
                 integrationSettings.IsEnabled(IntegrationKeys.HostDiscovery))
        {
            await freeDiscovery.IngestCountryBatchAsync(countryCode, cancellationToken).ConfigureAwait(false);
        }

        var after = await hostRepository.CountByCountryAsync(countryCode, cancellationToken).ConfigureAwait(false);
        var added = Math.Max(0, after - before);

        if (added > 0)
        {
            logger.LogDebug("Host prefetch {Country}: +{Added} (total {Total})", countryCode, added, after);
        }

        return added;
    }

    public async Task<int> WarmShodanRegionsAsync(
        string countryCode,
        CountryIngestStateEntity state,
        CancellationToken cancellationToken)
    {
        if (!integrationSettings.IsEnabled(IntegrationKeys.Shodan) ||
            !shodanOptions.Value.Enabled ||
            !shodanClient.IsConfigured ||
            shodanClient.IsSearchBlocked)
        {
            state.ShodanWarmComplete = true;
            return 0;
        }

        if (!CountryBoundingBoxCatalog.TryGet(countryCode, out var bbox))
        {
            state.ShodanWarmComplete = true;
            return 0;
        }

        var regions = ShodanRegionGrid.GetRegions(bbox);
        if (regions.Count == 0)
        {
            state.ShodanWarmComplete = true;
            return 0;
        }

        if (state.ShodanWarmComplete && state.PrefetchWarmComplete)
        {
            var refreshIndex = state.ShodanRegionIndex % regions.Count;
            var region = regions[refreshIndex];
            await shodanClient.GetRegionHostsAsync(region, cancellationToken).ConfigureAwait(false);
            state.ShodanRegionIndex = refreshIndex + 1;
            return 1;
        }

        var batch = Math.Clamp(prefetchOptions.Value.ShodanRegionsPerBatch, 1, 4);
        var warmed = 0;
        var index = Math.Clamp(state.ShodanRegionIndex, 0, regions.Count);

        for (var i = 0; i < batch && index < regions.Count; i++, index++)
        {
            await shodanClient.GetRegionHostsAsync(regions[index], cancellationToken).ConfigureAwait(false);
            warmed++;
        }

        state.ShodanRegionIndex = index;
        state.ShodanWarmComplete = index >= regions.Count;
        return warmed;
    }
}
