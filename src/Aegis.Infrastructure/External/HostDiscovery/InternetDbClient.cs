using System.Text.Json;
using Aegis.Infrastructure.Resilience;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Aegis.Infrastructure.External.HostDiscovery;

public sealed class InternetDbClient(
    IHttpClientFactory httpClientFactory,
    IOptions<HostDiscoveryOptions> options,
    IMemoryCache cache,
    ILogger<InternetDbClient> logger)
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromHours(24);

    public async Task<IReadOnlyList<int>> GetOpenPortsAsync(string ip, CancellationToken cancellationToken = default)
    {
        if (!options.Value.UseInternetDb)
        {
            return [];
        }

        var cacheKey = $"internetdb:ports:{ip}";
        if (cache.TryGetValue(cacheKey, out IReadOnlyList<int>? cached) && cached is not null)
        {
            return cached;
        }

        try
        {
            var client = httpClientFactory.CreateClient(HttpClientNames.InternetDb);
            using var response = await client.GetAsync(ip, cancellationToken).ConfigureAwait(false);
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                cache.Set(cacheKey, Array.Empty<int>(), CacheTtl);
                return [];
            }

            if (!response.IsSuccessStatusCode)
            {
                return [];
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("ports", out var portsProp) ||
                portsProp.ValueKind != JsonValueKind.Array)
            {
                cache.Set(cacheKey, Array.Empty<int>(), CacheTtl);
                return [];
            }

            var ports = portsProp.EnumerateArray()
                .Where(p => p.ValueKind == JsonValueKind.Number)
                .Select(p => p.GetInt32())
                .Distinct()
                .ToList();

            cache.Set(cacheKey, ports, CacheTtl);
            return ports;
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "InternetDB lookup failed for {Ip}", ip);
            return [];
        }
    }
}
