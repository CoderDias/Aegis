using Aegis.Application.Dtos.Geo;

namespace Aegis.Application.Geo;

public static class ViewportTileGrid
{
    public const double DefaultMaxTileAreaDeg2 = 0.11;
    public const int DefaultMaxTiles = 30;

    public static double TileSizeDegrees(int zoom, double maxAreaDeg2 = DefaultMaxTileAreaDeg2)
    {
        var maxSide = Math.Sqrt(maxAreaDeg2);
        return zoom switch
        {
            >= 14 => Math.Min(0.25, maxSide),
            >= 11 => Math.Min(0.35, maxSide),
            _ => maxSide
        };
    }

    public static int FetchZoomLevel(int viewportZoom) => Math.Min(Math.Max(viewportZoom, 4), 14);

    public static IReadOnlyList<BoundingBoxDto> GetTiles(
        BoundingBoxDto bbox,
        int zoom,
        int maxTiles = DefaultMaxTiles,
        double maxAreaDeg2 = DefaultMaxTileAreaDeg2)
    {
        var size = TileSizeDegrees(zoom, maxAreaDeg2);
        var tiles = new List<BoundingBoxDto>();

        var startLat = Math.Floor(bbox.South / size) * size;
        var startLng = Math.Floor(bbox.West / size) * size;

        for (var lat = startLat; lat < bbox.North + 1e-9; lat += size)
        {
            for (var lng = startLng; lng < bbox.East + 1e-9; lng += size)
            {
                tiles.Add(new BoundingBoxDto(
                    lat,
                    lng,
                    Math.Min(lat + size, 90),
                    Math.Min(lng + size, 180)));
            }
        }

        if (tiles.Count <= maxTiles)
        {
            return tiles;
        }

        var centerLat = (bbox.South + bbox.North) / 2;
        var centerLng = (bbox.West + bbox.East) / 2;

        return tiles
            .OrderBy(t => TileDistanceSquared(t, centerLat, centerLng))
            .Take(maxTiles)
            .ToList();
    }

    private static double TileDistanceSquared(BoundingBoxDto tile, double lat, double lng)
    {
        var tileCenterLat = (tile.South + tile.North) / 2;
        var tileCenterLng = (tile.West + tile.East) / 2;
        var dLat = tileCenterLat - lat;
        var dLng = tileCenterLng - lng;
        return dLat * dLat + dLng * dLng;
    }

    public static BoundingBoxDto SnapToTiles(BoundingBoxDto bbox, int zoom)
    {
        var tiles = GetTiles(bbox, zoom);
        if (tiles.Count == 0)
        {
            return bbox;
        }

        return new BoundingBoxDto(
            tiles.Min(t => t.South),
            tiles.Min(t => t.West),
            tiles.Max(t => t.North),
            tiles.Max(t => t.East));
    }

    public static string ComputeRegionHash(BoundingBoxDto bbox, int zoom)
    {
        var fetchZoom = FetchZoomLevel(zoom);
        var size = TileSizeDegrees(fetchZoom);
        var parts = GetTiles(bbox, fetchZoom)
            .Select(t => $"{(int)Math.Floor(t.South / size)}:{(int)Math.Floor(t.West / size)}")
            .Distinct()
            .OrderBy(p => p);

        return string.Join("|", parts);
    }

    public static string TileCacheKey(string prefix, BoundingBoxDto tile, int zoom) =>
        $"{prefix}:{zoom}:{tile.South:F2}:{tile.West:F2}";

    public static bool Contains(BoundingBoxDto outer, double lat, double lng) =>
        lat >= outer.South && lat <= outer.North && lng >= outer.West && lng <= outer.East;
}
