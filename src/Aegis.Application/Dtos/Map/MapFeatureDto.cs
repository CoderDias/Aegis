using Aegis.Application.Dtos.Geo;

namespace Aegis.Application.Dtos.Map;

public record MapFeatureDto(
    string OsmType,
    long OsmId,
    string? Name,
    string? Category,
    CoordinateDto Centroid,
    string? GeometryGeoJson,
    IReadOnlyDictionary<string, string> Tags);
