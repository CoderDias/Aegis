using System.Globalization;
using System.Text.Json;
using Aegis.Application.Abstractions;
using Aegis.Application.Dtos.Geo;
using Aegis.Application.Dtos.Intel;
using Aegis.Application.Geo;
using Aegis.Infrastructure.Options;
using Aegis.Infrastructure.Resilience;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Aegis.Infrastructure.External.Shodan;

public sealed class ShodanClient(
    IHttpClientFactory httpClientFactory,
    IServiceScopeFactory scopeFactory,
    IOptions<ShodanOptions> options,
    IOptions<CacheOptions> cacheOptions,
    IMemoryCache cache,
    ILogger<ShodanClient> logger) : IShodanDeviceService
{
    private const string BlockedKey = "shodan:blocked";
    private const string BlockedMessageKey = "shodan:blocked:message";
    private const string ApiInfoCacheKey = "shodan:api-info";

    public string? LastSearchMessage { get; private set; }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(options.Value.ApiKey);

    public bool IsSearchBlocked => cache.TryGetValue(BlockedMessageKey, out _);

    public async Task<IReadOnlyList<ShodanHostDto>> SearchInViewportAsync(
        BoundingBoxDto bbox,
        int zoom,
        CancellationToken cancellationToken = default)
    {
        _ = zoom;
        LastSearchMessage = null;

        if (!IsConfigured)
        {
            logger.LogDebug("Shodan skipped: configure Shodan:ApiKey.");
            return [];
        }

        if (cache.TryGetValue(BlockedMessageKey, out string? blockedMessage) &&
            !string.IsNullOrWhiteSpace(blockedMessage))
        {
            LastSearchMessage = blockedMessage;
            return [];
        }

        var regions = ShodanRegionGrid.GetRegions(bbox);
        var merged = new Dictionary<string, ShodanHostDto>(StringComparer.OrdinalIgnoreCase);

        foreach (var region in regions)
        {
            if (cache.TryGetValue(BlockedMessageKey, out _))
            {
                break;
            }

            var regionHosts = await GetRegionHostsAsync(region, cancellationToken).ConfigureAwait(false);
            foreach (var host in regionHosts)
            {
                if (host.Lat >= bbox.South && host.Lat <= bbox.North &&
                    host.Lng >= bbox.West && host.Lng <= bbox.East)
                {
                    merged.TryAdd(host.Ip, host);
                }
            }
        }

        if (merged.Count == 0 && string.IsNullOrEmpty(LastSearchMessage))
        {
            LastSearchMessage = "Nenhum host Shodan nesta região para a consulta atual.";
        }

        return merged.Values.ToList();
    }

    internal async Task<IReadOnlyList<ShodanHostDto>> GetRegionHostsAsync(
        BoundingBoxDto region,
        CancellationToken cancellationToken)
    {
        var query = await BuildRegionQueryAsync(region, cancellationToken).ConfigureAwait(false);
        var cacheKey = $"{ShodanRegionGrid.RegionCacheKey(region)}:{query.GetHashCode(StringComparison.Ordinal):X8}";
        if (cache.TryGetValue(cacheKey, out IReadOnlyList<ShodanHostDto>? cached) && cached is not null)
        {
            return cached;
        }

        var client = httpClientFactory.CreateClient(HttpClientNames.Shodan);
        var ttl = TimeSpan.FromHours(cacheOptions.Value.ShodanRegionTtlHours);
        var maxResults = Math.Clamp(options.Value.MaxResults, 1, 1000);
        var collected = new List<ShodanHostDto>();
        var page = 1;

        try
        {
            while (collected.Count < maxResults)
            {
                var url = BuildSearchUrl(query, page);
                using var response = await client.GetAsync(url, cancellationToken).ConfigureAwait(false);
                var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

                if (!response.IsSuccessStatusCode)
                {
                    var apiError = ParseApiError(json) ?? response.ReasonPhrase;
                    if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
                    {
                        LastSearchMessage = await BuildSearchBlockedMessageAsync(apiError, cancellationToken)
                            .ConfigureAwait(false);
                        cache.Set(BlockedKey, true, TimeSpan.FromMinutes(15));
                        cache.Set(BlockedMessageKey, LastSearchMessage, TimeSpan.FromMinutes(15));
                        logger.LogWarning("Shodan search blocked: {Message}", LastSearchMessage);
                    }
                    else
                    {
                        LastSearchMessage = $"Shodan: {apiError} (HTTP {(int)response.StatusCode}).";
                        logger.LogWarning(
                            "Shodan region search failed: {StatusCode} query={Query} error={Error}",
                            response.StatusCode,
                            query,
                            apiError);
                    }

                    cache.Set(cacheKey, Array.Empty<ShodanHostDto>(), TimeSpan.FromMinutes(5));
                    return [];
                }

                var pageHosts = ParseHosts(json);
                collected.AddRange(pageHosts);

                if (pageHosts.Count < 100 || collected.Count >= maxResults)
                {
                    break;
                }

                page++;
            }

            var hosts = collected.Take(maxResults).ToList();
            cache.Set(cacheKey, hosts, ttl);
            logger.LogInformation(
                "Shodan cached ({Query}): {Count} hosts, TTL {Hours}h",
                query,
                hosts.Count,
                cacheOptions.Value.ShodanRegionTtlHours);
            return hosts;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Shodan region request failed.");
            LastSearchMessage = "Falha ao consultar a API Shodan.";
            return [];
        }
    }

    private string BuildSearchUrl(string query, int page) =>
        $"/shodan/host/search?key={Uri.EscapeDataString(options.Value.ApiKey)}" +
        $"&query={Uri.EscapeDataString(query)}&page={page}&minify=true";

    private async Task<string> BuildRegionQueryAsync(BoundingBoxDto region, CancellationToken cancellationToken)
    {
        var centerLat = (region.South + region.North) / 2;
        var centerLng = (region.West + region.East) / 2;

        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var geocoding = scope.ServiceProvider.GetRequiredService<IGeocodingService>();
            var geocode = await geocoding
                .ReverseAsync(new CoordinateDto(centerLat, centerLng), cancellationToken)
                .ConfigureAwait(false);

            if (geocode?.AddressParts is not null)
            {
                var country = geocode.AddressParts.GetValueOrDefault("country_code")?.ToUpperInvariant();
                var city = geocode.AddressParts.GetValueOrDefault("city")
                    ?? geocode.AddressParts.GetValueOrDefault("town")
                    ?? geocode.AddressParts.GetValueOrDefault("municipality")
                    ?? geocode.AddressParts.GetValueOrDefault("village")
                    ?? geocode.AddressParts.GetValueOrDefault("state_district");

                if (!string.IsNullOrWhiteSpace(country) && !string.IsNullOrWhiteSpace(city))
                {
                    return FormattableString.Invariant($"country:{country} city:{FormatCityFilter(city)}");
                }

                if (!string.IsNullOrWhiteSpace(country))
                {
                    return FormattableString.Invariant($"country:{country}");
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Shodan geocode fallback to geo search.");
        }

        var radiusKm = EstimateRegionRadiusKm(region);
        return string.Create(CultureInfo.InvariantCulture, $"geo:{centerLat},{centerLng},{radiusKm}");
    }

    internal static string FormatCityFilter(string city)
    {
        var trimmed = city.Trim();
        return trimmed.Contains(' ', StringComparison.Ordinal) ? $"\"{trimmed}\"" : trimmed;
    }

    public async Task<bool> ProbeAsync(CancellationToken cancellationToken = default)
    {
        var info = await GetApiInfoAsync(cancellationToken).ConfigureAwait(false);
        return info is not null;
    }

    public async Task<ShodanApiInfoDto?> GetApiInfoAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(options.Value.ApiKey))
        {
            return null;
        }

        if (cache.TryGetValue(ApiInfoCacheKey, out ShodanApiInfoDto? cachedInfo) && cachedInfo is not null)
        {
            return cachedInfo;
        }

        try
        {
            var client = httpClientFactory.CreateClient(HttpClientNames.Shodan);
            using var response = await client
                .GetAsync($"/api-info?key={Uri.EscapeDataString(options.Value.ApiKey)}", cancellationToken)
                .ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var plan = root.TryGetProperty("plan", out var planProp) ? planProp.GetString() ?? "unknown" : "unknown";
            var queryCredits = root.TryGetProperty("query_credits", out var qcProp) ? qcProp.GetInt32() : 0;
            var scanCredits = root.TryGetProperty("scan_credits", out var scProp) ? scProp.GetInt32() : 0;
            var searchAvailable = !string.Equals(plan, "oss", StringComparison.OrdinalIgnoreCase) && queryCredits > 0;

            var info = new ShodanApiInfoDto(plan, queryCredits, scanCredits, searchAvailable);
            cache.Set(ApiInfoCacheKey, info, TimeSpan.FromMinutes(10));
            return info;
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Shodan api-info failed.");
            return null;
        }
    }

    private async Task<string> BuildSearchBlockedMessageAsync(string? apiError, CancellationToken cancellationToken)
    {
        var info = await GetApiInfoAsync(cancellationToken).ConfigureAwait(false);
        if (info is { SearchAvailable: false })
        {
            return
                $"Shodan: plano \"{info.Plan}\" (query credits: {info.QueryCredits}). " +
                "A busca /shodan/host/search exige Shodan Membership — upgrade em shodan.io/account.";
        }

        return string.IsNullOrWhiteSpace(apiError)
            ? "Shodan: busca indisponível para esta chave (plano ou créditos)."
            : $"Shodan: {apiError}";
    }

    internal static IReadOnlyList<ShodanHostDto> ParseHosts(string json)
    {
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("matches", out var matches) ||
            matches.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var list = new List<ShodanHostDto>();
        foreach (var match in matches.EnumerateArray())
        {
            if (!match.TryGetProperty("location", out var loc))
            {
                continue;
            }

            if (!loc.TryGetProperty("latitude", out var latProp) ||
                !loc.TryGetProperty("longitude", out var lngProp))
            {
                continue;
            }

            var ip = match.TryGetProperty("ip_str", out var ipProp) ? ipProp.GetString() : null;
            if (string.IsNullOrEmpty(ip))
            {
                continue;
            }

            var org = match.TryGetProperty("org", out var orgProp) ? orgProp.GetString() : null;
            var product = match.TryGetProperty("product", out var prodProp) ? prodProp.GetString() : null;
            int? port = match.TryGetProperty("port", out var portProp) && portProp.ValueKind == JsonValueKind.Number
                ? portProp.GetInt32()
                : null;
            var transport = match.TryGetProperty("transport", out var transportProp)
                ? transportProp.GetString()
                : null;

            string? hostnames = null;
            if (match.TryGetProperty("hostnames", out var hnProp) && hnProp.ValueKind == JsonValueKind.Array)
            {
                hostnames = string.Join(", ", hnProp.EnumerateArray()
                    .Select(h => h.GetString())
                    .Where(h => !string.IsNullOrEmpty(h)));
            }

            var city = loc.TryGetProperty("city", out var cityProp) ? cityProp.GetString() : null;
            var country = loc.TryGetProperty("country_name", out var countryProp)
                ? countryProp.GetString()
                : loc.TryGetProperty("country_code", out var codeProp) ? codeProp.GetString() : null;

            list.Add(new ShodanHostDto(
                ip,
                latProp.GetDouble(),
                lngProp.GetDouble(),
                org,
                product,
                port,
                hostnames,
                city,
                country,
                transport));
        }

        return list;
    }

    private static string? ParseApiError(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("error", out var errorProp))
            {
                return errorProp.GetString();
            }
        }
        catch (JsonException)
        {
            return json.Length > 120 ? json[..120] : json;
        }

        return null;
    }

    private static double EstimateRegionRadiusKm(BoundingBoxDto region)
    {
        var latSpan = Math.Abs(region.North - region.South);
        var lngSpan = Math.Abs(region.East - region.West);
        var deg = Math.Max(latSpan, lngSpan) / 2;
        return Math.Clamp(deg * 111.0 * 0.85, 80, 250);
    }
}
