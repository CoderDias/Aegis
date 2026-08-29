using Aegis.Application.Dtos.Geo;
using Aegis.Application.Dtos.Map;
using Aegis.Application.Geo;
using Aegis.Infrastructure.Data.Entities;
using Aegis.Infrastructure.External.Overpass;
using Aegis.Infrastructure.Geo;
using Aegis.Infrastructure.Options;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Aegis.Infrastructure.Background;

public sealed class OverpassCountryWarmer(
    OverpassClient overpassClient,
    IMemoryCache memoryCache,
    IOptions<CacheOptions> cacheOptions,
    IOptions<RegionalPrefetchOptions> prefetchOptions,
    IOptions<OverpassOptions> overpassOptions,
    ILogger<OverpassCountryWarmer> logger)
{
    private static readonly OverpassLayerKind[] WarmLayers =
    [
        OverpassLayerKind.Poi,
        OverpassLayerKind.Buildings,
        OverpassLayerKind.Roads,
        OverpassLayerKind.RadioTowers,
        OverpassLayerKind.Repeaters
    ];

    public async Task<(int TilesProcessed, int TilesTotal, bool WarmComplete)> WarmNextBatchAsync(
        string countryCode,
        CountryIngestStateEntity state,
        CancellationToken cancellationToken)
    {
        if (!CountryBoundingBoxCatalog.TryGet(countryCode, out var bbox))
        {
            return (0, 0, true);
        }

        var zoom = Math.Clamp(prefetchOptions.Value.OverpassFetchZoom, 4, 12);
        var tiles = ViewportTileGrid.GetTiles(
            bbox,
            zoom,
            maxTiles: 10_000,
            overpassOptions.Value.MaxBboxAreaDeg2);

        var total = tiles.Count;
        if (total == 0)
        {
            state.OverpassWarmComplete = true;
            return (0, 0, true);
        }

        if (state.OverpassWarmComplete && state.PrefetchWarmComplete)
        {
            return await RefreshExpiredTilesAsync(tiles, zoom, state, cancellationToken).ConfigureAwait(false);
        }

        var batchSize = Math.Clamp(prefetchOptions.Value.OverpassTilesPerBatch, 1, 8);
        var start = Math.Clamp(state.OverpassTileIndex, 0, total);
        var end = Math.Min(start + batchSize, total);
        var processed = 0;

        for (var i = start; i < end; i++)
        {
            try
            {
                await WarmTileAsync(tiles[i], zoom, cancellationToken).ConfigureAwait(false);
                processed++;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogDebug(ex, "Overpass prefetch tile skipped.");
            }
        }

        state.OverpassTileIndex = end;
        state.OverpassWarmComplete = end >= total;

        if (processed > 0)
        {
            logger.LogDebug(
                "Overpass prefetch {Country}: tiles {Done}/{Total}",
                countryCode,
                end,
                total);
        }

        return (processed, total, state.OverpassWarmComplete);
    }

    private async Task<(int TilesProcessed, int TilesTotal, bool WarmComplete)> RefreshExpiredTilesAsync(
        IReadOnlyList<BoundingBoxDto> tiles,
        int zoom,
        CountryIngestStateEntity state,
        CancellationToken cancellationToken)
    {
        var batchSize = Math.Clamp(prefetchOptions.Value.OverpassTilesPerBatch, 1, 8);
        var start = tiles.Count == 0 ? 0 : state.OverpassTileIndex % tiles.Count;
        var processed = 0;

        for (var offset = 0; offset < batchSize && offset < tiles.Count; offset++)
        {
            var index = (start + offset) % tiles.Count;
            var tile = tiles[index];
            if (IsTileFullyCached(tile, zoom))
            {
                continue;
            }

            await WarmTileAsync(tile, zoom, cancellationToken).ConfigureAwait(false);
            processed++;
        }

        if (tiles.Count > 0)
        {
            state.OverpassTileIndex = (start + batchSize) % tiles.Count;
        }

        return (processed, tiles.Count, true);
    }

    private bool IsTileFullyCached(BoundingBoxDto tile, int zoom)
    {
        foreach (var layer in WarmLayers)
        {
            var cacheKey = ViewportTileGrid.TileCacheKey(
                $"overpass:{layer.ToString().ToLowerInvariant()}",
                tile,
                zoom);

            if (!memoryCache.TryGetValue(cacheKey, out IReadOnlyList<MapFeatureDto>? _))
            {
                return false;
            }
        }

        return true;
    }

    private async Task WarmTileAsync(BoundingBoxDto tile, int zoom, CancellationToken cancellationToken)
    {
        var ttl = TimeSpan.FromSeconds(Math.Max(cacheOptions.Value.OverpassTtlSeconds, 60));

        foreach (var layer in WarmLayers)
        {
            var cacheKey = ViewportTileGrid.TileCacheKey(
                $"overpass:{layer.ToString().ToLowerInvariant()}",
                tile,
                zoom);

            if (memoryCache.TryGetValue(cacheKey, out _))
            {
                continue;
            }

            try
            {
                var features = await overpassClient
                    .QueryFeaturesAsync(tile, zoom, layer, cancellationToken)
                    .ConfigureAwait(false);
                memoryCache.Set(cacheKey, features, ttl);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogDebug(ex, "Overpass prefetch tile failed ({Layer}).", layer);
            }
        }
    }
}
