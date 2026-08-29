using Aegis.Application.Dtos.Flights;
using Aegis.Application.Dtos.Geo;
using Aegis.Application.Dtos.Intel;
using Aegis.Application.Dtos.Map;

namespace Aegis.Web.Components.Map;

public sealed class MapDetailViewModel
{
    public sealed record FieldItem(string Label, string? Value);

    public string Kind { get; init; } = "";
    public string Id { get; init; } = "";
    public string Title { get; init; } = "";
    public string Badge { get; init; } = "";
    public IReadOnlyList<FieldItem> Fields { get; init; } = [];
    public CoordinateDto? Location { get; init; }
    public MapFeatureDto? Feature { get; init; }
    public AircraftMarkerDto? Aircraft { get; init; }
    public ShodanHostDto? Host { get; init; }
    public NewsItemDto? News { get; init; }
    public RansomwareVictimDto? Ransomware { get; init; }
    public GeoMarkerDto? GeoMarker { get; init; }
    public string? ExternalUrl { get; init; }
    public string MetadataJson { get; init; } = "{}";
}
