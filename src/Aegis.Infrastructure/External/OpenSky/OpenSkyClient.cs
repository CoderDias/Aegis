using System.Globalization;
using Aegis.Domain.ValueObjects;
using Aegis.Infrastructure.Resilience;

namespace Aegis.Infrastructure.External.OpenSky;

public sealed class OpenSkyClient(IHttpClientFactory httpClientFactory)
{
    public async Task<string> GetStatesRawAsync(BoundingBox bbox, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(bbox);

        var (lamin, lomin, lamax, lomax) = bbox.ToOpenSkyParams();
        var client = httpClientFactory.CreateClient(HttpClientNames.OpenSky);

        var url = string.Create(CultureInfo.InvariantCulture,
            $"states/all?lamin={lamin:F6}&lomin={lomin:F6}&lamax={lamax:F6}&lomax={lomax:F6}");

        using var response = await client.GetAsync(url, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<string>?> GetRouteAirportsAsync(
        string callsign,
        CancellationToken cancellationToken = default)
    {
        var normalized = callsign.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return null;
        }

        var client = httpClientFactory.CreateClient(HttpClientNames.OpenSky);
        var url = $"routes?callsign={Uri.EscapeDataString(normalized)}";

        using var response = await client.GetAsync(url, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        return ParseRouteAirports(json);
    }

    internal static IReadOnlyList<string>? ParseRouteAirports(string json)
    {
        using var doc = System.Text.Json.JsonDocument.Parse(json);
        if (doc.RootElement.ValueKind != System.Text.Json.JsonValueKind.Array ||
            doc.RootElement.GetArrayLength() == 0)
        {
            return null;
        }

        var first = doc.RootElement[0];
        if (!first.TryGetProperty("route", out var routeProp) ||
            routeProp.ValueKind != System.Text.Json.JsonValueKind.Array ||
            routeProp.GetArrayLength() < 2)
        {
            return null;
        }

        var departure = routeProp[0].GetString()?.Trim().ToUpperInvariant();
        var arrival = routeProp[1].GetString()?.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(departure) || string.IsNullOrWhiteSpace(arrival))
        {
            return null;
        }

        return [departure, arrival];
    }
}
