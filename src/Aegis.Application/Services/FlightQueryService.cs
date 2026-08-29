using Aegis.Application.Abstractions;
using Aegis.Application.Dtos.Flights;
using Aegis.Application.Dtos.Geo;
using Aegis.Application.Mapping;

namespace Aegis.Application.Services;

public sealed class FlightQueryService(
    IFlightTrackingService flightTracking,
    IFlightSnapshotStore snapshotStore,
    IFlightRouteResolver routeResolver)
{
    public Task<IReadOnlyList<AircraftMarkerDto>> GetCurrentInViewportAsync(
        BoundingBoxDto bbox,
        FlightFilterDto filters,
        CancellationToken cancellationToken = default) =>
        flightTracking.GetCurrentInViewportAsync(bbox, filters, cancellationToken);

    public async Task<IReadOnlyList<AircraftMarkerDto>> GetTrackAsync(
        string icao24,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken = default)
    {
        var points = await snapshotStore
            .GetTrackAsync(icao24, from, to, cancellationToken)
            .ConfigureAwait(false);

        return points.Select(p => p.ToMarkerDto()).ToList();
    }

    public Task<AircraftMarkerDto?> GetByIcaoAsync(
        string icao24,
        CancellationToken cancellationToken = default) =>
        flightTracking.GetByIcaoAsync(icao24, cancellationToken);

    public Task<FlightRouteDto?> GetRouteAsync(
        AircraftMarkerDto aircraft,
        CancellationToken cancellationToken = default) =>
        routeResolver.ResolveAsync(aircraft, cancellationToken);
}
