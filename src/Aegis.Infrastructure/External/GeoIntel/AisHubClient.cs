using System.Globalization;
using System.Text.Json;
using Aegis.Application.Dtos.Geo;
using Aegis.Application.Dtos.Intel;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Aegis.Infrastructure.External.GeoIntel;

public sealed class AisHubClient(
    IHttpClientFactory httpClientFactory,
    IOptions<GeoIntelOptions> options,
    ILogger<AisHubClient> logger)
{
    public bool IsConfigured => !string.IsNullOrWhiteSpace(options.Value.AisHubUsername);

    public async Task<IReadOnlyList<GeoMarkerDto>> FetchInBboxAsync(
        BoundingBoxDto bbox,
        CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
        {
            return [];
        }

        try
        {
            var username = options.Value.AisHubUsername!.Trim();
            var url =
                $"http://data.aishub.net/ws.php?username={Uri.EscapeDataString(username)}" +
                "&format=1&output=json" +
                $"&latmin={bbox.South:F4}&latmax={bbox.North:F4}" +
                $"&lonmin={bbox.West:F4}&lonmax={bbox.East:F4}";

            var client = httpClientFactory.CreateClient("GeoIntel");
            client.Timeout = TimeSpan.FromSeconds(25);
            using var response = await client.GetAsync(url, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogDebug("AIS Hub HTTP {Status}", response.StatusCode);
                return [];
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);

            if (doc.RootElement.ValueKind != JsonValueKind.Array)
            {
                return [];
            }

            var max = Math.Clamp(options.Value.MaxShipMarkers, 50, 5000);
            var markers = new List<GeoMarkerDto>();

            foreach (var item in doc.RootElement.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                if (TryGetString(item, "ERROR", out var error) && !string.IsNullOrWhiteSpace(error))
                {
                    logger.LogWarning("AIS Hub error: {Error}", error);
                    return [];
                }

                if (!TryGetDouble(item, "LATITUDE", out var lat) ||
                    !TryGetDouble(item, "LONGITUDE", out var lng))
                {
                    continue;
                }

                var mmsi = TryGetString(item, "MMSI", out var mmsiValue) ? mmsiValue : "";
                TryGetString(item, "NAME", out var name);
                name = name?.Trim();
                TryGetString(item, "SOG", out var speed);
                TryGetString(item, "COG", out var course);

                markers.Add(new GeoMarkerDto(
                    string.IsNullOrWhiteSpace(mmsi) ? Guid.NewGuid().ToString("N") : mmsi,
                    "ship",
                    string.IsNullOrWhiteSpace(name) ? mmsi : name!,
                    speed is not null ? $"{speed} kn" : null,
                    lat,
                    lng,
                    1.2,
                    null,
                    course is not null ? $"Curso {course}°" : null));
            }

            return markers.Take(max).ToList();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogDebug(ex, "AIS Hub fetch failed");
            return [];
        }
    }

    private static bool TryGetString(JsonElement item, string key, out string? value)
    {
        value = null;
        if (!item.TryGetProperty(key, out var prop))
        {
            foreach (var p in item.EnumerateObject())
            {
                if (string.Equals(p.Name, key, StringComparison.OrdinalIgnoreCase))
                {
                    prop = p.Value;
                    break;
                }
            }
        }

        if (prop.ValueKind == JsonValueKind.String)
        {
            value = prop.GetString();
            return true;
        }

        if (prop.ValueKind == JsonValueKind.Number)
        {
            value = prop.GetRawText();
            return true;
        }

        return false;
    }

    private static bool TryGetDouble(JsonElement item, string key, out double value)
    {
        value = 0;
        if (!item.TryGetProperty(key, out var prop))
        {
            foreach (var p in item.EnumerateObject())
            {
                if (string.Equals(p.Name, key, StringComparison.OrdinalIgnoreCase))
                {
                    prop = p.Value;
                    break;
                }
            }
        }

        if (prop.ValueKind == JsonValueKind.Number)
        {
            value = prop.GetDouble();
            return true;
        }

        if (prop.ValueKind == JsonValueKind.Undefined)
        {
            return false;
        }

        if (prop.ValueKind == JsonValueKind.String &&
            double.TryParse(prop.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out value))
        {
            return true;
        }

        return false;
    }
}
