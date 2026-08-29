namespace Aegis.Application.Dtos.Flights;

public record FlightRouteEndpointDto(string Icao, string Label, double Lat, double Lng);

public record FlightRoutePointDto(double Lat, double Lng);

public record FlightRouteDto(
    FlightRouteEndpointDto? Origin,
    FlightRouteEndpointDto? Destination,
    IReadOnlyList<FlightRoutePointDto> Path,
    IReadOnlyList<FlightRoutePointDto>? FlownTrack,
    bool IsEstimated);
