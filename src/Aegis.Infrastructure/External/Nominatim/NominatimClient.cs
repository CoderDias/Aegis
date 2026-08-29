using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Aegis.Application.Dtos.Geo;
using Aegis.Infrastructure.External.Nominatim;
using Aegis.Infrastructure.Resilience;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Aegis.Infrastructure.External.Nominatim;

public sealed class NominatimClient
{
    private static readonly SemaphoreSlim RateLimiter = new(1, 1);
    private static DateTimeOffset _lastRequestUtc = DateTimeOffset.MinValue;

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly NominatimOptions _options;
    private readonly ILogger<NominatimClient> _logger;

    public NominatimClient(
        IHttpClientFactory httpClientFactory,
        IOptions<NominatimOptions> options,
        ILogger<NominatimClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<IReadOnlyList<GeocodeResultDto>> SearchAsync(
        string query,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var normalized = NormalizeQuery(query);
        if (normalized.Length < _options.MinSearchLength)
        {
            return [];
        }

        var effectiveLimit = Math.Clamp(limit, 1, _options.MaxResults);
        var url = string.Create(CultureInfo.InvariantCulture,
            $"/search?q={Uri.EscapeDataString(normalized)}&format=jsonv2&limit={effectiveLimit}&addressdetails=1");

        var json = await GetStringAsync(url, cancellationToken).ConfigureAwait(false);
        return ParseSearchResults(json);
    }

    public async Task<GeocodeResultDto?> ReverseAsync(
        CoordinateDto coordinate,
        CancellationToken cancellationToken = default)
    {
        var url = string.Create(CultureInfo.InvariantCulture,
            $"/reverse?lat={coordinate.Lat:F6}&lon={coordinate.Lng:F6}&format=jsonv2&zoom=18&addressdetails=1");

        var json = await GetStringAsync(url, cancellationToken).ConfigureAwait(false);
        return ParseReverseResult(json);
    }

    public static string ComputeForwardHash(string normalizedQuery) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"forward:{normalizedQuery}"))).ToLowerInvariant();

    public static string ComputeReverseHash(CoordinateDto coordinate) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            $"reverse:{Math.Round(coordinate.Lat, 5):F5},{Math.Round(coordinate.Lng, 5):F5}"))).ToLowerInvariant();

    public static string NormalizeQuery(string query) =>
        string.Join(' ', query.Trim().ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries));

    private async Task<string> GetStringAsync(string relativeUrl, CancellationToken cancellationToken)
    {
        await EnforceRateLimitAsync(cancellationToken).ConfigureAwait(false);

        var client = _httpClientFactory.CreateClient(HttpClientNames.Nominatim);
        using var response = await client.GetAsync(relativeUrl, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task EnforceRateLimitAsync(CancellationToken cancellationToken)
    {
        await RateLimiter.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var elapsed = DateTimeOffset.UtcNow - _lastRequestUtc;
            if (elapsed < TimeSpan.FromSeconds(1))
            {
                await Task.Delay(TimeSpan.FromSeconds(1) - elapsed, cancellationToken).ConfigureAwait(false);
            }

            _lastRequestUtc = DateTimeOffset.UtcNow;
        }
        finally
        {
            RateLimiter.Release();
        }
    }

    internal static IReadOnlyList<GeocodeResultDto> ParseSearchResults(string json)
    {
        using var document = JsonDocument.Parse(json);
        if (document.RootElement.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var results = new List<GeocodeResultDto>();
        foreach (var element in document.RootElement.EnumerateArray())
        {
            var mapped = MapElement(element);
            if (mapped is not null)
            {
                results.Add(mapped);
            }
        }

        return results;
    }

    internal static GeocodeResultDto? ParseReverseResult(string json)
    {
        using var document = JsonDocument.Parse(json);
        if (document.RootElement.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        if (document.RootElement.TryGetProperty("error", out _))
        {
            return null;
        }

        return MapElement(document.RootElement);
    }

    private static GeocodeResultDto? MapElement(JsonElement element)
    {
        if (!element.TryGetProperty("lat", out var latProp) ||
            !element.TryGetProperty("lon", out var lonProp))
        {
            return null;
        }

        if (!double.TryParse(latProp.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var lat) ||
            !double.TryParse(lonProp.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var lng))
        {
            return null;
        }

        var displayName = element.TryGetProperty("display_name", out var displayProp)
            ? displayProp.GetString() ?? $"{lat},{lng}"
            : $"{lat},{lng}";

        var type = element.TryGetProperty("type", out var typeProp) ? typeProp.GetString() : null;
        long? osmIdValue = element.TryGetProperty("osm_id", out var osmIdProp) && osmIdProp.TryGetInt64(out var parsedOsmId)
            ? parsedOsmId
            : null;

        IReadOnlyDictionary<string, string>? addressParts = null;
        if (element.TryGetProperty("address", out var addressProp) && addressProp.ValueKind == JsonValueKind.Object)
        {
            addressParts = addressProp.EnumerateObject()
                .Where(p => p.Value.ValueKind == JsonValueKind.String)
                .ToDictionary(p => p.Name, p => p.Value.GetString() ?? string.Empty);
        }

        return new GeocodeResultDto(displayName, new CoordinateDto(lat, lng), type, osmIdValue, addressParts);
    }
}
