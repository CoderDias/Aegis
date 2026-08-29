using Aegis.Application.Dtos.Flights;

namespace Aegis.Application.Abstractions;

public interface IFlightRouteResolver
{
    Task<FlightRouteDto?> ResolveAsync(
        AircraftMarkerDto aircraft,
        CancellationToken cancellationToken = default);
}
