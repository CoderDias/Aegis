using System.Globalization;
using System.Text.Json;
using Aegis.Application.Dtos.Flights;
using Aegis.Infrastructure.Resilience;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Aegis.Infrastructure.External.AirStream;

public sealed class AirStreamClient(
    IHttpClientFactory httpClientFactory,
    IOptions<AirStreamOptions> options,
    ILogger<AirStreamClient> logger)
{
    public async Task<IReadOnlyList<AircraftMarkerDto>> GetAircraftInRadiusAsync(
        double centerLat,
        double centerLng,
        int radiusNm,
        CancellationToken cancellationToken = default)
    {
        if (!options.Value.Enabled)
        {
            return [];
        }

        var client = httpClientFactory.CreateClient(HttpClientNames.AirStream);
        var url = string.Create(
            CultureInfo.InvariantCulture,
            $"v3/lat/{centerLat:F4}/lon/{centerLng:F4}/dist/{radiusNm}");

        try
        {
            using var response = await client.GetAsync(url, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogDebug("AirStream returned {StatusCode}", response.StatusCode);
                return [];
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            return ParseAircraft(json);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "AirStream request failed.");
            return [];
        }
    }

    internal static IReadOnlyList<AircraftMarkerDto> ParseAircraft(string json)
    {
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("ac", out var ac) || ac.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var list = new List<AircraftMarkerDto>();
        foreach (var item in ac.EnumerateArray())
        {
            if (!item.TryGetProperty("lat", out var latProp) ||
                !item.TryGetProperty("lon", out var lonProp))
            {
                continue;
            }

            var lat = latProp.GetDouble();
            var lon = lonProp.GetDouble();
            if (Math.Abs(lat) < 0.0001 && Math.Abs(lon) < 0.0001)
            {
                continue;
            }

            var hex = item.TryGetProperty("hex", out var hexProp) ? hexProp.GetString()?.ToLowerInvariant() : null;
            if (string.IsNullOrEmpty(hex))
            {
                continue;
            }

            double? alt = item.TryGetProperty("alt_baro", out var altProp) && altProp.ValueKind == JsonValueKind.Number
                ? altProp.GetDouble()
                : null;
            double? vel = item.TryGetProperty("gs", out var gsProp) && gsProp.ValueKind == JsonValueKind.Number
                ? gsProp.GetDouble()
                : null;
            double? track = item.TryGetProperty("track", out var trProp) && trProp.ValueKind == JsonValueKind.Number
                ? trProp.GetDouble()
                : null;
            var callsign = item.TryGetProperty("flight", out var flProp) ? flProp.GetString()?.Trim() : null;
            var country = item.TryGetProperty("r", out var rProp) ? rProp.GetString() : null;

            list.Add(new AircraftMarkerDto(
                hex,
                callsign,
                lat,
                lon,
                alt,
                vel,
                track,
                country,
                false,
                DateTimeOffset.UtcNow));
        }

        return list;
    }
}
