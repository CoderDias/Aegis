using Aegis.Domain.Entities;
using Aegis.Domain.Enums;
using Aegis.Domain.Services;
using Aegis.Domain.ValueObjects;
using FluentAssertions;

namespace Aegis.Domain.Tests;

public class GeofenceEvaluatorTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 25, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void EvaluateTransition_EmitsEnteredOnce_WhenAircraftRemainsInside()
    {
        var investigationId = Guid.NewGuid();
        var geofence = Geofence.Create(
            Guid.NewGuid(),
            investigationId,
            "Approach zone",
            """{"type":"Circle","center":[-46.6333,-23.5505],"radiusMeters":50000}""",
            Now);

        var outside = Coordinate.Create(-23.0, -46.0);
        var inside = Coordinate.Create(-23.5505, -46.6333);

        GeofenceEvaluator.EvaluateTransition(outside, inside, geofence)
            .Should().Be(GeofenceTransition.Entered);

        GeofenceEvaluator.EvaluateTransition(inside, inside, geofence)
            .Should().Be(GeofenceTransition.None);
    }

    [Fact]
    public void EvaluateTransition_ReturnsExited_WhenLeavingGeofence()
    {
        var investigationId = Guid.NewGuid();
        var geofence = Geofence.Create(
            Guid.NewGuid(),
            investigationId,
            "Approach zone",
            """{"type":"Circle","center":[-46.6333,-23.5505],"radiusMeters":50000}""",
            Now);

        var inside = Coordinate.Create(-23.5505, -46.6333);
        var outside = Coordinate.Create(-23.0, -46.0);

        GeofenceEvaluator.EvaluateTransition(inside, outside, geofence)
            .Should().Be(GeofenceTransition.Exited);
    }
}
