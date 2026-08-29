namespace Aegis.Application.Dtos.Flights;

public record FlightFilterDto(
    double? MinAltitude = null,
    double? MaxAltitude = null,
    double? MinVelocity = null,
    double? MaxVelocity = null,
    string? CallsignContains = null,
    bool? OnGround = null,
    string? OriginCountry = null);
