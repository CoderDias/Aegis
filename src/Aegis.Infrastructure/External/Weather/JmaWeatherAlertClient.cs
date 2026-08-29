using System.Net.Http.Json;
using System.Text.Json;
using Aegis.Application.Dtos.Intel;
using Aegis.Infrastructure.Geo;
using Microsoft.Extensions.Logging;

namespace Aegis.Infrastructure.External.Weather;

public sealed class JmaWeatherAlertClient(
    IHttpClientFactory httpClientFactory,
    ILogger<JmaWeatherAlertClient> logger)
{
    private static readonly Dictionary<string, string> WarningLabels = new(StringComparer.Ordinal)
    {
        ["02"] = "Alerta de nevasca",
        ["03"] = "Alerta de chuva forte",
        ["04"] = "Alerta de neve",
        ["05"] = "Alerta de vento",
        ["06"] = "Alerta de neve intensa",
        ["07"] = "Alerta de ondas",
        ["08"] = "Alerta de mar agitado",
        ["10"] = "Aviso de chuva forte",
        ["12"] = "Aviso de neve",
        ["13"] = "Aviso de neve intensa",
        ["14"] = "Aviso de trovoada",
        ["15"] = "Aviso de vento forte",
        ["16"] = "Aviso de neve",
        ["17"] = "Aviso de geada",
        ["18"] = "Aviso de deslizamento",
        ["19"] = "Aviso de avalanche",
        ["20"] = "Aviso de neblina",
        ["21"] = "Aviso de seca",
        ["22"] = "Aviso de tsunami",
        ["23"] = "Aviso de mar agitado",
        ["24"] = "Aviso de mar de ressaca",
        ["25"] = "Aviso de maré alta",
        ["26"] = "Aviso de acúmulo de neve",
        ["27"] = "Aviso de ondas",
        ["28"] = "Aviso de baixa temperatura",
        ["29"] = "Alerta de chuva torrencial",
        ["32"] = "Alerta especial de nevasca",
        ["33"] = "Alerta especial de chuva torrencial",
        ["35"] = "Alerta especial de vento",
        ["36"] = "Alerta especial de neve intensa",
        ["43"] = "Alerta de chuva perigosa"
    };

    private readonly SemaphoreSlim _centroidLock = new(1, 1);
    private Dictionary<string, (double Lat, double Lng, string Name)>? _centroidsByCode;

    public async Task<IReadOnlyList<GeoMarkerDto>> FetchActiveAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var client = httpClientFactory.CreateClient("JmaAlerts");
            var bulletins = await client
                .GetFromJsonAsync<JsonElement[]>("bosai/warning/data/r8/map.json", cancellationToken)
                .ConfigureAwait(false);

            if (bulletins is null || bulletins.Length == 0)
            {
                return [];
            }

            var centroids = await GetCentroidsAsync(cancellationToken).ConfigureAwait(false);
            var markers = new List<GeoMarkerDto>();
            var seen = new HashSet<string>(StringComparer.Ordinal);

            foreach (var bulletin in bulletins)
            {
                if (!bulletin.TryGetProperty("warning", out var warning))
                {
                    continue;
                }

                var reportTime = bulletin.TryGetProperty("reportDatetime", out var reportElement) &&
                                 DateTimeOffset.TryParse(reportElement.GetString(), out var parsedReport)
                    ? parsedReport
                    : (DateTimeOffset?)null;
                var headline = bulletin.TryGetProperty("headlineText", out var headlineElement)
                    ? headlineElement.GetString()
                    : null;

                AddFromItems(warning, "class10Items", centroids, markers, seen, reportTime, headline);
                AddFromItems(warning, "class20Items", centroids, markers, seen, reportTime, headline);
            }

            return markers;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            logger.LogDebug(ex, "JMA avisos indisponível.");
            return [];
        }
    }

    private static void AddFromItems(
        JsonElement warning,
        string propertyName,
        IReadOnlyDictionary<string, (double Lat, double Lng, string Name)> centroids,
        List<GeoMarkerDto> markers,
        HashSet<string> seen,
        DateTimeOffset? reportTime,
        string? headline)
    {
        if (!warning.TryGetProperty(propertyName, out var items))
        {
            return;
        }

        foreach (var item in items.EnumerateArray())
        {
            if (!item.TryGetProperty("areaCode", out var areaCodeElement))
            {
                continue;
            }

            var areaCode = areaCodeElement.GetString();
            if (string.IsNullOrWhiteSpace(areaCode) || !centroids.TryGetValue(areaCode, out var centroid))
            {
                continue;
            }

            if (!item.TryGetProperty("kinds", out var kinds))
            {
                continue;
            }

            foreach (var kind in kinds.EnumerateArray())
            {
                var code = kind.TryGetProperty("code", out var codeElement) ? codeElement.GetString() : null;
                var status = kind.TryGetProperty("status", out var statusElement) ? statusElement.GetString() : null;
                if (string.IsNullOrWhiteSpace(code) && string.IsNullOrWhiteSpace(status))
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(status) &&
                    status.Contains("解除", StringComparison.Ordinal))
                {
                    continue;
                }

                var dedupeKey = $"{areaCode}:{code}:{status}";
                if (!seen.Add(dedupeKey))
                {
                    continue;
                }

                var label = !string.IsNullOrWhiteSpace(code) && WarningLabels.TryGetValue(code, out var mapped)
                    ? mapped
                    : status ?? headline ?? "Aviso meteorológico JMA";

                markers.Add(new GeoMarkerDto(
                    $"jma-{areaCode}-{code ?? status}",
                    "weather_alert",
                    label,
                    status,
                    centroid.Lat,
                    centroid.Lng,
                    Weight: code is "33" or "35" or "36" or "43" ? 3 : 2,
                    Timestamp: reportTime,
                    Detail: headline,
                    Source: "JMA · Ásia",
                    Severity: status,
                    Region: centroid.Name,
                    EventType: label,
                    Risks: headline));
            }
        }
    }

    private async Task<IReadOnlyDictionary<string, (double Lat, double Lng, string Name)>> GetCentroidsAsync(
        CancellationToken cancellationToken)
    {
        if (_centroidsByCode is not null)
        {
            return _centroidsByCode;
        }

        await _centroidLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_centroidsByCode is not null)
            {
                return _centroidsByCode;
            }

            var client = httpClientFactory.CreateClient("JmaAlerts");
            var payload = await client
                .GetFromJsonAsync<JsonElement>("bosai/common/const/geojson/class10s.json", cancellationToken)
                .ConfigureAwait(false);

            var map = new Dictionary<string, (double Lat, double Lng, string Name)>(StringComparer.Ordinal);
            if (payload.TryGetProperty("features", out var features))
            {
                foreach (var feature in features.EnumerateArray())
                {
                    if (!feature.TryGetProperty("properties", out var props) ||
                        !props.TryGetProperty("code", out var codeElement) ||
                        !feature.TryGetProperty("geometry", out var geometry))
                    {
                        continue;
                    }

                    var code = codeElement.GetString();
                    if (string.IsNullOrWhiteSpace(code))
                    {
                        continue;
                    }

                    var name = props.TryGetProperty("name", out var nameElement)
                        ? nameElement.GetString() ?? code
                        : code;
                    var centroid = PolygonCentroidHelper.FromGeoJson(geometry);
                    if (centroid is not null)
                    {
                        map[code] = (centroid.Value.Lat, centroid.Value.Lng, name);
                    }
                }
            }

            _centroidsByCode = map;
            return map;
        }
        finally
        {
            _centroidLock.Release();
        }
    }
}
