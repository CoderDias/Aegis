using System.Globalization;
using System.Text;
using System.Text.Json;
using Aegis.Application.Dtos.Geo;
using Aegis.Application.Dtos.Map;
using Aegis.Infrastructure.Resilience;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Aegis.Infrastructure.External.Overpass;

public sealed class OverpassClient
{
    private readonly SemaphoreSlim _concurrency;
    private readonly object _throttleLock = new();
    private DateTimeOffset _lastRequestUtc = DateTimeOffset.MinValue;

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly OverpassOptions _options;
    private readonly ILogger<OverpassClient> _logger;

    public OverpassClient(
        IHttpClientFactory httpClientFactory,
        IOptions<OverpassOptions> options,
        ILogger<OverpassClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _logger = logger;
        var maxConcurrent = Math.Clamp(_options.MaxConcurrentRequests, 1, 8);
        _concurrency = new SemaphoreSlim(maxConcurrent, maxConcurrent);
    }

    public async Task<IReadOnlyList<MapFeatureDto>> QueryFeaturesAsync(
        BoundingBoxDto bbox,
        int zoom,
        OverpassLayerKind layer,
        CancellationToken cancellationToken = default)
    {
        if (zoom < _options.MinZoom && layer is OverpassLayerKind.Buildings or OverpassLayerKind.Roads)
        {
            return [];
        }

        var area = OverpassQueries.ComputeBboxAreaDeg2(bbox.South, bbox.West, bbox.North, bbox.East);
        if (area > _options.MaxBboxAreaDeg2 + 1e-6)
        {
            return [];
        }

        var query = OverpassQueries.BuildQuery(
            bbox.South,
            bbox.West,
            bbox.North,
            bbox.East,
            zoom,
            _options.MaxFeatures,
            layer);

        if (string.IsNullOrWhiteSpace(query))
        {
            return [];
        }

        try
        {
            var json = await ExecuteQueryAsync(query, cancellationToken).ConfigureAwait(false);
            return ParseFeatures(json);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogDebug(ex, "Overpass {Layer} query failed; returning empty.", layer);
            return [];
        }
    }

    public async Task<IReadOnlyList<MapFeatureDto>> QueryFeaturesAsync(
        BoundingBoxDto bbox,
        int zoom,
        CancellationToken cancellationToken = default) =>
        await QueryFeaturesAsync(bbox, zoom, OverpassLayerKind.Buildings, cancellationToken).ConfigureAwait(false);

    public async Task<bool> ProbeAsync(CancellationToken cancellationToken = default)
    {
        const string query = "[out:json][timeout:5];node(1);out count;";
        try
        {
            _ = await ExecuteQueryAsync(query, cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Overpass probe failed.");
            return false;
        }
    }

    private async Task<string> ExecuteQueryAsync(string query, CancellationToken cancellationToken)
    {
        await _concurrency.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await ThrottleAsync(cancellationToken).ConfigureAwait(false);

            var primaryBase = _options.BaseUrl.TrimEnd('/');
            var fallbackBase = _options.FallbackBaseUrl.TrimEnd('/');
            var timeout = TimeSpan.FromSeconds(Math.Clamp(_options.RequestTimeoutSeconds, 5, 30));

            if (string.IsNullOrWhiteSpace(fallbackBase) ||
                string.Equals(primaryBase, fallbackBase, StringComparison.OrdinalIgnoreCase))
            {
                return await PostQueryAsync(primaryBase, query, timeout, cancellationToken).ConfigureAwait(false);
            }

            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            linked.CancelAfter(timeout);

            var primaryTask = PostQueryAsync(primaryBase, query, timeout, linked.Token);
            var fallbackTask = PostQueryAsync(fallbackBase, query, timeout, linked.Token);

            var winner = await Task.WhenAny(primaryTask, fallbackTask).ConfigureAwait(false);
            try
            {
                return await winner.ConfigureAwait(false);
            }
            catch
            {
                var loser = ReferenceEquals(winner, primaryTask) ? fallbackTask : primaryTask;
                return await loser.ConfigureAwait(false);
            }
        }
        finally
        {
            _concurrency.Release();
        }
    }

    private async Task ThrottleAsync(CancellationToken cancellationToken)
    {
        TimeSpan wait = TimeSpan.Zero;
        lock (_throttleLock)
        {
            var minInterval = TimeSpan.FromMilliseconds(Math.Clamp(_options.MinRequestIntervalMs, 50, 2000));
            var elapsed = DateTimeOffset.UtcNow - _lastRequestUtc;
            if (elapsed < minInterval)
            {
                wait = minInterval - elapsed;
            }
        }

        if (wait > TimeSpan.Zero)
        {
            await Task.Delay(wait, cancellationToken).ConfigureAwait(false);
        }

        lock (_throttleLock)
        {
            _lastRequestUtc = DateTimeOffset.UtcNow;
        }
    }

    private async Task<string> PostQueryAsync(
        string baseUrl,
        string query,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient(HttpClientNames.Overpass);
        using var content = new StringContent(
            $"data={Uri.EscapeDataString(query)}",
            Encoding.UTF8,
            "application/x-www-form-urlencoded");
        using var request = new HttpRequestMessage(HttpMethod.Post, baseUrl) { Content = content };
        using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
    }

    internal static IReadOnlyList<MapFeatureDto> ParseFeatures(string json)
    {
        using var document = JsonDocument.Parse(json);
        if (!document.RootElement.TryGetProperty("elements", out var elements) ||
            elements.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var features = new List<MapFeatureDto>();

        foreach (var element in elements.EnumerateArray())
        {
            var feature = MapElement(element);
            if (feature is not null)
            {
                features.Add(feature);
            }
        }

        return features;
    }

    private static MapFeatureDto? MapElement(JsonElement element)
    {
        if (!element.TryGetProperty("type", out var typeProp))
        {
            return null;
        }

        var osmType = typeProp.GetString();
        if (string.IsNullOrWhiteSpace(osmType))
        {
            return null;
        }

        if (!element.TryGetProperty("id", out var idProp) || !idProp.TryGetInt64(out var osmId))
        {
            return null;
        }

        var tags = element.TryGetProperty("tags", out var tagsProp) && tagsProp.ValueKind == JsonValueKind.Object
            ? tagsProp.EnumerateObject()
                .Where(p => p.Value.ValueKind == JsonValueKind.String)
                .ToDictionary(p => p.Name, p => p.Value.GetString() ?? string.Empty)
            : new Dictionary<string, string>();

        tags.TryGetValue("name", out var name);
        var category = tags.TryGetValue("highway", out var highway) ? highway :
            tags.TryGetValue("amenity", out var amenity) ? amenity :
            tags.TryGetValue("shop", out var shop) ? shop :
            tags.TryGetValue("tourism", out var tourism) ? tourism :
            tags.TryGetValue("building", out var building) ? building :
            tags.TryGetValue("aeroway", out var aeroway) ? aeroway : null;

        if (!TryGetCentroid(element, out var lat, out var lng))
        {
            return null;
        }

        string? geometryGeoJson = null;
        if (element.TryGetProperty("geometry", out var geometryProp) && geometryProp.ValueKind == JsonValueKind.Array)
        {
            var coordinates = geometryProp.EnumerateArray()
                .Select(p => new[] { GetDouble(p, "lon"), GetDouble(p, "lat") })
                .ToArray();

            if (coordinates.Length >= 3 &&
                (osmType == "way" || osmType == "relation") &&
                MapFeatureLayers.ShouldRenderAsPolygon(tags, coordinates.Length, osmType))
            {
                if (coordinates[0][0] != coordinates[^1][0] || coordinates[0][1] != coordinates[^1][1])
                {
                    coordinates = coordinates.Append(coordinates[0]).ToArray();
                }

                geometryGeoJson = JsonSerializer.Serialize(new
                {
                    type = "Polygon",
                    coordinates = new[] { coordinates }
                });
            }
            else if (coordinates.Length >= 2)
            {
                if (coordinates.Length >= 3)
                {
                    geometryGeoJson = JsonSerializer.Serialize(new
                    {
                        type = "LineString",
                        coordinates
                    });
                }
                else
                {
                    geometryGeoJson = JsonSerializer.Serialize(new
                    {
                        type = "Point",
                        coordinates = coordinates[0]
                    });
                }
            }
        }
        else if (element.TryGetProperty("lat", out var latProp) && element.TryGetProperty("lon", out var lonProp))
        {
            geometryGeoJson = JsonSerializer.Serialize(new
            {
                type = "Point",
                coordinates = new[] { lonProp.GetDouble(), latProp.GetDouble() }
            });
        }

        return new MapFeatureDto(
            osmType,
            osmId,
            name,
            category,
            new CoordinateDto(lat, lng),
            geometryGeoJson,
            tags);
    }

    private static bool TryGetCentroid(JsonElement element, out double lat, out double lng)
    {
        if (element.TryGetProperty("center", out var center) &&
            center.TryGetProperty("lat", out var centerLat) &&
            center.TryGetProperty("lon", out var centerLon))
        {
            lat = centerLat.GetDouble();
            lng = centerLon.GetDouble();
            return true;
        }

        if (element.TryGetProperty("lat", out var latProp) &&
            element.TryGetProperty("lon", out var lonProp))
        {
            lat = latProp.GetDouble();
            lng = lonProp.GetDouble();
            return true;
        }

        if (element.TryGetProperty("geometry", out var geometry) && geometry.ValueKind == JsonValueKind.Array)
        {
            double sumLat = 0;
            double sumLng = 0;
            var count = 0;

            foreach (var point in geometry.EnumerateArray())
            {
                sumLat += GetDouble(point, "lat");
                sumLng += GetDouble(point, "lon");
                count++;
            }

            if (count > 0)
            {
                lat = sumLat / count;
                lng = sumLng / count;
                return true;
            }
        }

        lat = 0;
        lng = 0;
        return false;
    }

    private static double GetDouble(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var prop) ? prop.GetDouble() : 0d;
}
