using System.Text.Json;
using Aegis.Infrastructure.Resilience;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Aegis.Infrastructure.External.HostDiscovery;

public sealed class CountryCidrProvider(
    IHttpClientFactory httpClientFactory,
    IMemoryCache cache,
    ILogger<CountryCidrProvider> logger)
{
    private const string CachePrefix = "hostdiscovery:cidr:";

    public async Task<IReadOnlyList<CidrBlock>> GetCountryBlocksAsync(
        string countryCode,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(countryCode) || countryCode.Length != 2)
        {
            return [];
        }

        var code = countryCode.ToUpperInvariant();
        var cacheKey = CachePrefix + code;
        if (cache.TryGetValue(cacheKey, out IReadOnlyList<CidrBlock>? cached) && cached is not null)
        {
            return cached;
        }

        var blocks = await LoadFromRipeStatAsync(code, cancellationToken).ConfigureAwait(false);
        if (blocks.Count == 0)
        {
            blocks = await LoadFromIpDenyAsync(code.ToLowerInvariant(), cancellationToken).ConfigureAwait(false);
        }

        if (blocks.Count > 0)
        {
            cache.Set(cacheKey, blocks, TimeSpan.FromDays(7));
            logger.LogInformation("Loaded {Count} CIDR blocks for country {Country}", blocks.Count, code);
        }
        else
        {
            logger.LogWarning("No CIDR blocks available for country {Country}", code);
        }

        return blocks;
    }

    private async Task<IReadOnlyList<CidrBlock>> LoadFromRipeStatAsync(
        string countryCode,
        CancellationToken cancellationToken)
    {
        try
        {
            var client = httpClientFactory.CreateClient(HttpClientNames.RipeStat);
            using var response = await client
                .GetAsync($"data/country-resource-list/data.json?resource={countryCode.ToLowerInvariant()}", cancellationToken)
                .ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                return [];
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("data", out var data) ||
                !data.TryGetProperty("resources", out var resources) ||
                !resources.TryGetProperty("ipv4", out var ipv4) ||
                ipv4.ValueKind != JsonValueKind.Array)
            {
                return [];
            }

            var blocks = new List<CidrBlock>();
            foreach (var prefix in ipv4.EnumerateArray())
            {
                var line = prefix.GetString();
                if (!string.IsNullOrWhiteSpace(line) && CidrBlock.TryParse(line, out var block))
                {
                    blocks.Add(block);
                }
            }

            return blocks;
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "RIPEstat CIDR load failed for {Country}", countryCode);
            return [];
        }
    }

    private async Task<IReadOnlyList<CidrBlock>> LoadFromIpDenyAsync(
        string countryCode,
        CancellationToken cancellationToken)
    {
        try
        {
            var client = httpClientFactory.CreateClient(HttpClientNames.IpDeny);
            using var response = await client
                .GetAsync($"ipdata/files/ipv4/country/{countryCode}.zone", cancellationToken)
                .ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                return [];
            }

            var text = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            var blocks = new List<CidrBlock>();
            foreach (var line in text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (CidrBlock.TryParse(line, out var block))
                {
                    blocks.Add(block);
                }
            }

            return blocks;
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "IPDeny CIDR load failed for {Country}", countryCode);
            return [];
        }
    }
}
