using Aegis.Application.Dtos.Geo;
using Aegis.Application.Dtos.Intel;
using Aegis.Infrastructure.External.Overpass;
using Microsoft.Extensions.Options;

namespace Aegis.Infrastructure.External.GeoIntel;

/// <summary>
/// Fallback quando AIS Hub não está configurado: portos, balsas e embarcações mapeadas no OSM.
/// </summary>
public sealed class OsmVesselFallbackClient(
    OverpassClient overpass,
    IOptions<GeoIntelOptions> options)
{
    public async Task<IReadOnlyList<GeoMarkerDto>> FetchInBboxAsync(
        BoundingBoxDto bbox,
        int zoom,
        CancellationToken cancellationToken = default)
    {
        if (zoom < 6)
        {
            return [];
        }

        var features = await overpass
            .QueryFeaturesAsync(bbox, zoom, OverpassLayerKind.OsmVessels, cancellationToken)
            .ConfigureAwait(false);

        var max = Math.Clamp(options.Value.MaxShipMarkers, 50, 5000);
        return features
            .Select(f => new GeoMarkerDto(
                $"{f.OsmType}/{f.OsmId}",
                "ship",
                f.Name ?? f.Tags.GetValueOrDefault("seamark:type") ?? "Embarcação/porto",
                f.Category,
                f.Centroid.Lat,
                f.Centroid.Lng,
                1.0,
                null,
                f.Tags.GetValueOrDefault("route") is "ferry" ? "Rota de balsa (OSM)" : "OSM — configure AIS Hub para posições ao vivo"))
            .Take(max)
            .ToList();
    }
}
