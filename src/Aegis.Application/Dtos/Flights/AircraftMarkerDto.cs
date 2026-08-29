namespace Aegis.Application.Dtos.Flights;

public record AircraftMarkerDto(
    string Icao24,
    string? Callsign,
    double Lat,
    double Lng,
    double? BaroAltitude,
    double? Velocity,
    double? Heading,
    string? OriginCountry,
    bool OnGround,
    DateTimeOffset LastContact);
