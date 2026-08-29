using Aegis.Application.Dtos.Geo;

namespace Aegis.Application.Dtos.Flights;

public record FlightSnapshot(
    DateTimeOffset CapturedAt,
    BoundingBoxDto Bbox,
    IReadOnlyList<AircraftMarkerDto> Aircraft);
