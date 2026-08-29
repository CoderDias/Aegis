using Aegis.Application.Dtos.Flights;
using Aegis.Application.Dtos.Geo;
using Aegis.Application.Dtos.Intel;
using Aegis.Application.Dtos.Map;

namespace Aegis.Application.Dtos.Map;

public record MapLayerState(
    bool Aircraft = true,
    bool Buildings = true,
    bool Roads = false,
    bool AreasOfInterest = true,
    bool Heatmap = false,
    bool Shodan = false,
    bool News = true,
    bool Ransomware = true,
    bool Ships = true,
    bool Seismic = true,
    bool RadioTowers = true,
    bool Repeaters = true,
    bool Erbs = false,
    bool PublicCameras = false,
    bool Ports = true,
    bool WeatherAlerts = true,
    bool InpeOverlay = false);

public record ViewportQueryRequest(
    BoundingBoxDto Bbox,
    int Zoom,
    MapLayerState Layers,
    FlightFilterDto Filters);

public record ViewportQueryResult(
    IReadOnlyList<AircraftMarkerDto> Aircraft,
    IReadOnlyList<MapFeatureDto> Features,
    IReadOnlyList<ShodanHostDto> ShodanHosts,
    IReadOnlyList<NewsItemDto> NewsItems,
    string? Hint);
