using Aegis.Application.Dtos.Flights;
using Aegis.Application.Dtos.Geo;

namespace Aegis.Application.Abstractions;

public interface IFlightTrackingService
{
    Task<IReadOnlyList<AircraftMarkerDto>> GetCurrentInViewportAsync(
        BoundingBoxDto bbox,
        FlightFilterDto filters,
        CancellationToken cancellationToken = default);

    Task<AircraftMarkerDto?> GetByIcaoAsync(string icao24, CancellationToken cancellationToken = default);
}
