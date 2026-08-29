using Aegis.Application.Abstractions;
using Aegis.Application.Dtos.Geo;
using Aegis.Application.Dtos.Intel;
using Aegis.Application.Settings;
using Aegis.Infrastructure.External.Censys;
using Aegis.Infrastructure.External.Shodan;
using Aegis.Infrastructure.Settings;
using Microsoft.Extensions.Options;

namespace Aegis.Infrastructure.External.HostDiscovery;

public sealed class CompositeHostDiscoveryService(
    ShodanClient shodanClient,
    CensysHostDiscoveryClient censysDiscovery,
    FreeHostDiscoveryClient freeDiscovery,
    IntegrationSettingsService integrationSettings,
    IOptions<ShodanOptions> shodanOptions,
    IOptions<CensysOptions> censysOptions) : IShodanDeviceService
{
    public string? LastSearchMessage { get; private set; }

    public bool IsConfigured =>
        shodanClient.IsConfigured ||
        (integrationSettings.IsEnabled(IntegrationKeys.Censys) && censysOptions.Value.Enabled) ||
        integrationSettings.IsEnabled(IntegrationKeys.HostDiscovery);

    public async Task<IReadOnlyList<ShodanHostDto>> SearchInViewportAsync(
        BoundingBoxDto bbox,
        int zoom,
        CancellationToken cancellationToken = default)
    {
        LastSearchMessage = null;

        if (integrationSettings.IsEnabled(IntegrationKeys.Shodan) &&
            shodanOptions.Value.Enabled &&
            shodanClient.IsConfigured &&
            !shodanClient.IsSearchBlocked)
        {
            var paidHosts = await shodanClient
                .SearchInViewportAsync(bbox, zoom, cancellationToken)
                .ConfigureAwait(false);

            if (paidHosts.Count > 0)
            {
                LastSearchMessage = shodanClient.LastSearchMessage;
                return paidHosts;
            }
        }

        if (integrationSettings.IsEnabled(IntegrationKeys.Censys) && censysOptions.Value.Enabled)
        {
            var censysHosts = await censysDiscovery
                .SearchInViewportAsync(bbox, zoom, cancellationToken)
                .ConfigureAwait(false);

            if (censysHosts.Count > 0)
            {
                LastSearchMessage = censysDiscovery.LastSearchMessage;
                return censysHosts;
            }
        }

        if (!integrationSettings.IsEnabled(IntegrationKeys.HostDiscovery))
        {
            LastSearchMessage = censysDiscovery.LastSearchMessage
                ?? shodanClient.LastSearchMessage
                ?? "Nenhuma fonte de hosts disponível.";
            return [];
        }

        var freeHosts = await freeDiscovery
            .SearchInViewportAsync(bbox, zoom, cancellationToken)
            .ConfigureAwait(false);

        LastSearchMessage = freeHosts.Count > 0
            ? freeDiscovery.LastSearchMessage
            : censysDiscovery.LastSearchMessage
              ?? freeDiscovery.LastSearchMessage
              ?? shodanClient.LastSearchMessage;

        return freeHosts;
    }

    public Task<bool> ProbeAsync(CancellationToken cancellationToken = default) =>
        shodanClient.ProbeAsync(cancellationToken);

    public Task<ShodanApiInfoDto?> GetApiInfoAsync(CancellationToken cancellationToken = default) =>
        shodanClient.GetApiInfoAsync(cancellationToken);
}
