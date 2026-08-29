using Aegis.Application.Abstractions;
using Aegis.Application.Dtos.Flights;
using Aegis.Application.Services;
using Aegis.Domain.Entities;
using Aegis.Domain.Enums;
using FluentAssertions;
using NSubstitute;

namespace Aegis.Application.Tests;

public class AlertingServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 25, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task EvaluateAsync_EmitsEnteredAlertOnce_WhenAircraftStaysInsideGeofence()
    {
        var investigation = Investigation.Create("Alert test", null, Now);
        var geofence = Geofence.Create(
            Guid.NewGuid(),
            investigation.Id,
            "Approach zone",
            """{"type":"Circle","center":[-46.6333,-23.5505],"radiusMeters":50000}""",
            Now);
        investigation.AddGeofence(geofence, Now);

        var store = Substitute.For<IInvestigationStore>();
        store.GetAsync(investigation.Id, Arg.Any<CancellationToken>())
            .Returns(investigation);

        var clock = Substitute.For<IClock>();
        clock.UtcNow.Returns(Now);

        var service = new AlertingService(store, clock);
        var aircraft = new List<AircraftMarkerDto>
        {
            new("abc123", "GLO123", -23.5505, -46.6333, 10_000, 250, 90, "Brazil", false, Now)
        };

        var first = await service.EvaluateAsync(investigation.Id, aircraft);
        var second = await service.EvaluateAsync(investigation.Id, aircraft);

        first.Should().HaveCount(1);
        first[0].Message.Should().Contain("GLO123");
        first[0].Category.Should().Be("aircraft");
        first[0].GeofenceName.Should().Be("Approach zone");

        second.Should().BeEmpty();
        await store.Received(1).SaveAsync(investigation, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task EvaluateAsync_ReturnsEmpty_WhenInvestigationNotFound()
    {
        var store = Substitute.For<IInvestigationStore>();
        store.GetAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((Investigation?)null);

        var clock = Substitute.For<IClock>();
        clock.UtcNow.Returns(Now);

        var service = new AlertingService(store, clock);
        var alerts = await service.EvaluateAsync(Guid.NewGuid(), []);

        alerts.Should().BeEmpty();
        await store.DidNotReceive().SaveAsync(Arg.Any<Investigation>(), Arg.Any<CancellationToken>());
    }
}
