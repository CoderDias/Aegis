using Aegis.Application.Dtos.Geo;

namespace Aegis.Application.Geo;

/// <summary>
/// Grade fixa (~2°) para cache regional do Shodan — independe do zoom do mapa.
/// </summary>
public static class ShodanRegionGrid
{
    public const double RegionSizeDegrees = 2.0;

    public static IReadOnlyList<BoundingBoxDto> GetRegions(BoundingBoxDto viewport)
    {
        var size = RegionSizeDegrees;
        var tiles = new List<BoundingBoxDto>();

        var startLat = Math.Floor(viewport.South / size) * size;
        var startLng = Math.Floor(viewport.West / size) * size;

        for (var lat = startLat; lat < viewport.North + 1e-9; lat += size)
        {
            for (var lng = startLng; lng < viewport.East + 1e-9; lng += size)
            {
                tiles.Add(new BoundingBoxDto(
                    lat,
                    lng,
                    Math.Min(lat + size, 90),
                    Math.Min(lng + size, 180)));
            }
        }

        return tiles;
    }

    public static string ComputeRegionKey(BoundingBoxDto viewport) =>
        string.Join("|", GetRegions(viewport)
            .Select(r => $"{r.South:F1}:{r.West:F1}")
            .Distinct()
            .OrderBy(k => k));

    public static string RegionCacheKey(BoundingBoxDto region) =>
        $"shodan:region:{region.South:F1}:{region.West:F1}";
}
