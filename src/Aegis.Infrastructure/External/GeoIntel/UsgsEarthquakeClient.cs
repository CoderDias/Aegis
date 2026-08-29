using System.Text.Json;
using Aegis.Application.Dtos.Intel;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Aegis.Infrastructure.External.GeoIntel;

public sealed class UsgsEarthquakeClient(
    IHttpClientFactory httpClientFactory,
    IOptions<GeoIntelOptions> options,
    ILogger<UsgsEarthquakeClient> logger)
{
    private const string FeedUrl = "https://earthquake.usgs.gov/earthquakes/feed/v1.0/summary/2.5_week.geojson";

    public async Task<IReadOnlyList<GeoMarkerDto>> FetchRecentAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var client = httpClientFactory.CreateClient("GeoIntel");
            using var response = await client.GetAsync(FeedUrl, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return [];
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);

            if (!doc.RootElement.TryGetProperty("features", out var features) ||
                features.ValueKind != JsonValueKind.Array)
            {
                return [];
            }

            var minMag = options.Value.SeismicMinMagnitude;
            var markers = new List<GeoMarkerDto>();

            foreach (var feature in features.EnumerateArray())
            {
                if (!feature.TryGetProperty("geometry", out var geometry) ||
                    !geometry.TryGetProperty("coordinates", out var coords) ||
                    coords.GetArrayLength() < 2)
                {
                    continue;
                }

                var lng = coords[0].GetDouble();
                var lat = coords[1].GetDouble();
                var props = feature.TryGetProperty("properties", out var p) ? p : default;

                var mag = props.ValueKind == JsonValueKind.Object && props.TryGetProperty("mag", out var magProp)
                    ? magProp.GetDouble()
                    : 0;

                if (mag < minMag)
                {
                    continue;
                }

                var id = feature.TryGetProperty("id", out var idProp) ? idProp.GetString() ?? Guid.NewGuid().ToString("N")
                    : Guid.NewGuid().ToString("N");
                var place = props.TryGetProperty("place", out var placeProp) ? placeProp.GetString() : null;
                var timeMs = props.TryGetProperty("time", out var timeProp) ? timeProp.GetInt64() : 0L;
                var timestamp = timeMs > 0
                    ? DateTimeOffset.FromUnixTimeMilliseconds(timeMs)
                    : (DateTimeOffset?)null;

                markers.Add(new GeoMarkerDto(
                    id,
                    "seismic",
                    $"M{mag:F1}",
                    place,
                    lat,
                    lng,
                    Math.Clamp(mag, 1, 10),
                    timestamp,
                    place));
            }

            return markers;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "USGS earthquake feed failed");
            return [];
        }
    }
}
