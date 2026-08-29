using Aegis.Application.Abstractions;
using Aegis.Application.Dtos.Settings;
using Aegis.Application.Settings;
using Aegis.Infrastructure.External.AirStream;
using Aegis.Infrastructure.External.Censys;
using Aegis.Infrastructure.External.OpenSky;
using Aegis.Infrastructure.External.Shodan;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Aegis.Infrastructure.Settings;

public sealed class IntegrationSettingsService(
    IServiceScopeFactory scopeFactory,
    IMemoryCache cache,
    IConfiguration configuration,
    IOptions<OpenSkyOptions> openSkyOptions,
    IOptions<AirStreamOptions> airStreamOptions,
    IOptions<ShodanOptions> shodanOptions,
    IOptions<CensysOptions> censysOptions)
{
    private const string CacheKey = "integration-settings:all";

    public bool IsEnabled(string key)
    {
        if (cache.TryGetValue(CacheKey, out Dictionary<string, IntegrationSettingDto>? map) &&
            map is not null &&
            map.TryGetValue(key, out var setting))
        {
            return setting.Enabled;
        }

        return GetDefaultEnabled(key);
    }

    public async Task<IReadOnlyList<IntegrationSettingDto>> ListAsync(CancellationToken cancellationToken = default)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IIntegrationSettingsStore>();
        var rows = await store.ListAsync(cancellationToken).ConfigureAwait(false);
        return rows
            .Select(row => row with { IsConfigured = IsConfigured(row.Key) })
            .OrderBy(row => row.SortOrder)
            .ThenBy(row => row.DisplayName)
            .ToList();
    }

    public async Task SetEnabledAsync(string key, bool enabled, CancellationToken cancellationToken = default)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IIntegrationSettingsStore>();
        await store.SetEnabledAsync(key, enabled, cancellationToken).ConfigureAwait(false);
        cache.Remove(CacheKey);
    }

    public async Task WarmCacheAsync(CancellationToken cancellationToken = default)
    {
        var rows = await ListAsync(cancellationToken).ConfigureAwait(false);
        cache.Set(
            CacheKey,
            rows.ToDictionary(row => row.Key, StringComparer.OrdinalIgnoreCase),
            TimeSpan.FromMinutes(10));
    }

    public bool IsConfigured(string key) => key.ToLowerInvariant() switch
    {
        IntegrationKeys.OpenSky =>
            !string.IsNullOrWhiteSpace(openSkyOptions.Value.ClientId) &&
            !string.IsNullOrWhiteSpace(openSkyOptions.Value.ClientSecret),
        IntegrationKeys.AirStream =>
            !string.IsNullOrWhiteSpace(airStreamOptions.Value.ApiToken),
        IntegrationKeys.Shodan =>
            !string.IsNullOrWhiteSpace(shodanOptions.Value.ApiKey),
        IntegrationKeys.Censys =>
            !string.IsNullOrWhiteSpace(censysOptions.Value.ApiToken),
        IntegrationKeys.HostDiscovery => true,
        IntegrationKeys.Rss => true,
        _ => false
    };

    private bool GetDefaultEnabled(string key) => key.ToLowerInvariant() switch
    {
        IntegrationKeys.OpenSky => true,
        IntegrationKeys.AirStream => airStreamOptions.Value.Enabled,
        IntegrationKeys.Shodan => shodanOptions.Value.Enabled,
        IntegrationKeys.Censys => censysOptions.Value.Enabled,
        IntegrationKeys.HostDiscovery => configuration.GetValue("HostDiscovery:Enabled", true),
        IntegrationKeys.Rss => true,
        _ => false
    };
}
