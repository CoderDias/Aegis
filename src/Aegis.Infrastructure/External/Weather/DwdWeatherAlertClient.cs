using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Aegis.Application.Dtos.Intel;
using Aegis.Infrastructure.Geo;
using Microsoft.Extensions.Logging;

namespace Aegis.Infrastructure.External.Weather;

public sealed class DwdWeatherAlertClient(
    IHttpClientFactory httpClientFactory,
    ILogger<DwdWeatherAlertClient> logger)
{
    private static readonly Regex JsonpWrapper = new(
        @"^warnWetter\.loadWarnings\((.*)\);?\s*$",
        RegexOptions.Singleline | RegexOptions.Compiled);

    private readonly SemaphoreSlim _centroidLock = new(1, 1);
    private Dictionary<int, (double Lat, double Lng)>? _centroidsByCell;

    public async Task<IReadOnlyList<GeoMarkerDto>> FetchActiveAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var client = httpClientFactory.CreateClient("DwdAlerts");
            using var response = await client.GetAsync(
                    "DWD/warnungen/warnapp_landkreise/json/warnings.json",
                    cancellationToken)
                .ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogDebug("DWD avisos retornou {Status}", response.StatusCode);
                return [];
            }

            var raw = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            var json = JsonpWrapper.Match(raw) is { Success: true } match ? match.Groups[1].Value : raw;
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("warnings", out var warningsElement))
            {
                return [];
            }

            var centroids = await GetCentroidsAsync(cancellationToken).ConfigureAwait(false);
            var markers = new List<GeoMarkerDto>();

            foreach (var property in warningsElement.EnumerateObject())
            {
                if (!int.TryParse(property.Name, out var cellId) ||
                    property.Value.ValueKind != JsonValueKind.Array ||
                    property.Value.GetArrayLength() == 0)
                {
                    continue;
                }

                if (!centroids.TryGetValue(cellId, out var centroid))
                {
                    continue;
                }

                foreach (var warning in property.Value.EnumerateArray())
                {
                    var headline = warning.TryGetProperty("headline", out var headlineElement)
                        ? headlineElement.GetString()
                        : null;
                    var description = warning.TryGetProperty("description", out var descriptionElement)
                        ? descriptionElement.GetString()
                        : null;
                    var eventName = warning.TryGetProperty("event", out var eventElement)
                        ? eventElement.GetString()
                        : null;
                    var regionName = warning.TryGetProperty("regionName", out var regionElement)
                        ? regionElement.GetString()
                        : null;
                    var state = warning.TryGetProperty("state", out var stateElement)
                        ? stateElement.GetString()
                        : null;
                    var instruction = warning.TryGetProperty("instruction", out var instructionElement)
                        ? instructionElement.GetString()
                        : null;
                    var level = warning.TryGetProperty("level", out var levelElement) &&
                                levelElement.TryGetInt32(out var levelValue)
                        ? levelValue
                        : 1;

                    var start = warning.TryGetProperty("start", out var startElement) &&
                                startElement.TryGetInt64(out var startMs)
                        ? DateTimeOffset.FromUnixTimeMilliseconds(startMs)
                        : (DateTimeOffset?)null;
                    var end = warning.TryGetProperty("end", out var endElement) &&
                              endElement.TryGetInt64(out var endMs)
                        ? DateTimeOffset.FromUnixTimeMilliseconds(endMs)
                        : (DateTimeOffset?)null;

                    var title = headline ?? eventName ?? "Aviso meteorológico DWD";
                    var region = string.Join(" · ", new[] { regionName, state }.Where(static x => !string.IsNullOrWhiteSpace(x)));

                    markers.Add(new GeoMarkerDto(
                        $"dwd-{cellId}-{eventName}-{start?.ToUnixTimeSeconds()}",
                        "weather_alert",
                        title,
                        eventName,
                        centroid.Lat,
                        centroid.Lng,
                        Weight: level,
                        Timestamp: start,
                        Detail: description,
                        Source: "DWD · Europa",
                        Severity: MapLevel(level),
                        Region: region,
                        ValidUntil: end,
                        EventType: eventName,
                        Instructions: instruction));
                }
            }

            return markers;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            logger.LogDebug(ex, "DWD avisos indisponível.");
            return [];
        }
    }

    private async Task<IReadOnlyDictionary<int, (double Lat, double Lng)>> GetCentroidsAsync(
        CancellationToken cancellationToken)
    {
        if (_centroidsByCell is not null)
        {
            return _centroidsByCell;
        }

        await _centroidLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_centroidsByCell is not null)
            {
                return _centroidsByCell;
            }

            var client = httpClientFactory.CreateClient("DwdGeo");
            using var response = await client.GetAsync(
                    "geoserver/dwd/ows?service=WFS&version=1.1.0&request=GetFeature&typeName=dwd:Warngebiete_Kreise&outputFormat=application/json",
                    cancellationToken)
                .ConfigureAwait(false);

            response.EnsureSuccessStatusCode();
            var payload = await response.Content
                .ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            var map = new Dictionary<int, (double Lat, double Lng)>();
            if (payload.TryGetProperty("features", out var features))
            {
                foreach (var feature in features.EnumerateArray())
                {
                    if (!feature.TryGetProperty("properties", out var props) ||
                        !props.TryGetProperty("WARNCELLID", out var idElement) ||
                        !idElement.TryGetInt32(out var cellId) ||
                        !feature.TryGetProperty("geometry", out var geometry))
                    {
                        continue;
                    }

                    var centroid = PolygonCentroidHelper.FromGeoJson(geometry);
                    if (centroid is not null)
                    {
                        map[cellId] = centroid.Value;
                    }
                }
            }

            _centroidsByCell = map;
            return map;
        }
        finally
        {
            _centroidLock.Release();
        }
    }

    private static string MapLevel(int level) => level switch
    {
        >= 4 => "Extremo",
        3 => "Severo",
        2 => "Moderado",
        _ => "Menor"
    };
}
