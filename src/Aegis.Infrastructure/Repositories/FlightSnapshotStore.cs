using Aegis.Application.Abstractions;
using Aegis.Domain.Entities;
using Aegis.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Aegis.Infrastructure.Repositories;

public sealed class FlightSnapshotStore(AegisDbContext db) : IFlightSnapshotStore
{
    public async Task InsertPointsAsync(
        IReadOnlyList<FlightTrackPoint> points,
        CancellationToken cancellationToken = default)
    {
        if (points.Count == 0)
        {
            return;
        }

        var normalized = points
            .Select(p => FlightTrackPoint.Create(
                0,
                p.Icao24,
                p.Time,
                p.Latitude,
                p.Longitude,
                p.Source,
                p.Callsign,
                p.BaroAltitude,
                p.GeoAltitude,
                p.Velocity,
                p.Heading,
                p.VerticalRate,
                p.OriginCountry,
                p.OnGround))
            .ToList();

        db.FlightTrackPoints.AddRange(normalized);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<FlightTrackPoint>> GetTrackAsync(
        string icao24,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken = default)
    {
        var normalized = icao24.Trim().ToLowerInvariant();

        return await db.FlightTrackPoints
            .AsNoTracking()
            .Where(p => p.Icao24 == normalized && p.Time >= from && p.Time <= to)
            .OrderBy(p => p.Time)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task PurgeOldAsync(DateTimeOffset olderThan, CancellationToken cancellationToken = default)
    {
        await db.FlightTrackPoints
            .Where(p => p.Time < olderThan)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
