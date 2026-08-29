using Aegis.Domain.Entities;

namespace Aegis.Application.Abstractions;

public interface IFlightSnapshotStore
{
    Task InsertPointsAsync(IReadOnlyList<FlightTrackPoint> points, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<FlightTrackPoint>> GetTrackAsync(
        string icao24,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken = default);

    Task PurgeOldAsync(DateTimeOffset olderThan, CancellationToken cancellationToken = default);
}
