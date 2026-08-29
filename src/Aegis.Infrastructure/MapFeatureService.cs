using Aegis.Application.Abstractions;
using Aegis.Application.Dtos.Geo;
using Aegis.Application.Dtos.Map;
using Aegis.Application.Geo;
using Aegis.Infrastructure.External.Overpass;
using Aegis.Infrastructure.Geo;
using Aegis.Infrastructure.Options;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Aegis.Infrastructure;

public sealed class MapFeatureService(
    OverpassClient overpassClient,
    StaticGovernmentPoiCatalog staticPois,
    RepeaterBookCatalog repeaterBook,
    AnatelErbCatalog erbCatalog,
    PublicCameraCatalog cameraCatalog,
    BrazilPortCatalog portCatalog,
    IMemoryCache memoryCache,
    IOptions<CacheOptions> cacheOptions,
    IOptions<OverpassOptions> overpassOptions,
    ILogger<MapFeatureService> logger) : IMapFeatureService
{
    private bool _overpassDegraded;

    public bool IsOverpassDegraded => _overpassDegraded;

    public async Task<IReadOnlyList<MapFeatureDto>> GetFeaturesAsync(
        BoundingBoxDto bbox,
        int zoom,
        MapLayerState layers,
        CancellationToken cancellationToken = default)
    {
        var fetchZoom = ViewportTileGrid.FetchZoomLevel(zoom);
        var maxArea = overpassOptions.Value.MaxBboxAreaDeg2;
        var tiles = ViewportTileGrid.GetTiles(bbox, fetchZoom, ViewportTileGrid.DefaultMaxTiles, maxArea);
        var merged = new Dictionary<string, MapFeatureDto>(StringComparer.OrdinalIgnoreCase);
        _overpassDegraded = false;

        if (layers.AreasOfInterest && zoom >= 8)
        {
            foreach (var poi in staticPois.GetInViewport(bbox))
            {
                var id = $"{poi.OsmType}/{poi.OsmId}";
                merged.TryAdd(id, poi);
            }
        }

        if (layers.Repeaters && zoom >= 7)
        {
            foreach (var repeater in repeaterBook.GetInViewport(bbox))
            {
                var id = $"{repeater.OsmType}/{repeater.OsmId}";
                merged.TryAdd(id, repeater);
            }
        }

        if (layers.Erbs && zoom >= 8)
        {
            foreach (var erb in erbCatalog.GetInViewport(bbox))
            {
                var id = $"{erb.OsmType}/{erb.OsmId}";
                merged.TryAdd(id, erb);
            }
        }

        if (layers.PublicCameras && zoom >= 9)
        {
            foreach (var camera in cameraCatalog.GetInViewport(bbox))
            {
                var id = $"{camera.OsmType}/{camera.OsmId}";
                merged.TryAdd(id, camera);
            }
        }

        if (layers.Ports && zoom >= 8)
        {
            foreach (var port in portCatalog.GetInViewport(bbox))
            {
                var id = $"{port.OsmType}/{port.OsmId}";
                merged.TryAdd(id, port);
            }
        }

        var layerTasks = new List<Task>();

        if (layers.AreasOfInterest && zoom >= 8)
        {
            layerTasks.Add(MergeLayerSafeAsync(tiles, fetchZoom, zoom, OverpassLayerKind.Poi, merged, cancellationToken));
        }

        if (layers.Buildings && zoom >= 8)
        {
            layerTasks.Add(MergeLayerSafeAsync(tiles, fetchZoom, zoom, OverpassLayerKind.Buildings, merged, cancellationToken));
        }

        if (layers.Roads && zoom >= 8)
        {
            layerTasks.Add(MergeLayerSafeAsync(tiles, fetchZoom, zoom, OverpassLayerKind.Roads, merged, cancellationToken));
        }

        if (layers.RadioTowers && zoom >= 7)
        {
            layerTasks.Add(MergeLayerSafeAsync(tiles, fetchZoom, zoom, OverpassLayerKind.RadioTowers, merged, cancellationToken));
        }

        if (layers.Repeaters && zoom >= 7)
        {
            layerTasks.Add(MergeLayerSafeAsync(tiles, fetchZoom, zoom, OverpassLayerKind.Repeaters, merged, cancellationToken));
        }

        if (layerTasks.Count > 0)
        {
            await Task.WhenAll(layerTasks).ConfigureAwait(false);
        }

        return merged.Values.ToList();
    }

    private async Task MergeLayerSafeAsync(
        IReadOnlyList<BoundingBoxDto> tiles,
        int fetchZoom,
        int zoom,
        OverpassLayerKind layer,
        Dictionary<string, MapFeatureDto> merged,
        CancellationToken cancellationToken)
    {
        try
        {
            await MergeLayerAsync(tiles, fetchZoom, zoom, layer, merged, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Overpass {Layer} query failed for viewport.", layer);
            _overpassDegraded = true;
        }
    }

    private async Task MergeLayerAsync(
        IReadOnlyList<BoundingBoxDto> tiles,
        int fetchZoom,
        int zoom,
        OverpassLayerKind layer,
        Dictionary<string, MapFeatureDto> merged,
        CancellationToken cancellationToken)
    {
        var maxParallel = Math.Clamp(overpassOptions.Value.MaxConcurrentRequests, 1, 8);

        await Parallel.ForEachAsync(
            tiles,
            new ParallelOptions
            {
                MaxDegreeOfParallelism = maxParallel,
                CancellationToken = cancellationToken
            },
            async (tile, ct) =>
            {
                var cacheKey = ViewportTileGrid.TileCacheKey(
                    $"overpass:{layer.ToString().ToLowerInvariant()}",
                    tile,
                    fetchZoom);

                if (!memoryCache.TryGetValue(cacheKey, out IReadOnlyList<MapFeatureDto>? tileFeatures))
                {
                    tileFeatures = await overpassClient
                        .QueryFeaturesAsync(tile, zoom, layer, ct)
                        .ConfigureAwait(false);
                    memoryCache.Set(
                        cacheKey,
                        tileFeatures,
                        TimeSpan.FromSeconds(cacheOptions.Value.OverpassTtlSeconds));
                }

                lock (merged)
                {
                    foreach (var feature in tileFeatures ?? [])
                    {
                        var id = $"{feature.OsmType}/{feature.OsmId}";
                        merged.TryAdd(id, feature);
                    }
                }
            }).ConfigureAwait(false);
    }
}
