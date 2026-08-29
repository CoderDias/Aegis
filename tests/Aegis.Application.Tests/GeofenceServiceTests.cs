using Aegis.Application.Abstractions;
using Aegis.Application.Services;
using Aegis.Domain.Entities;
using Aegis.Domain.Enums;
using FluentAssertions;
using NSubstitute;

namespace Aegis.Application.Tests;

public class GeofenceServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 26, 12, 0, 0, TimeSpan.Zero);

    private static Investigation CreateInvestigationWithGeofence(out Geofence geofence)
    {
        var investigation = Investigation.Create("Test Investigation", null, Now);
        geofence = Geofence.Create(
            Guid.NewGuid(),
            investigation.Id,
            "Zone Alpha",
            """{"type":"Circle","center":[-48.55,-27.59],"radiusMeters":5000}""",
            Now);
        investigation.AddGeofence(geofence, Now);
        return investigation;
    }

    [Fact]
    public async Task ToggleAsync_DisablesEnabledGeofence()
    {
        var investigation = CreateInvestigationWithGeofence(out var geofence);
        geofence.IsEnabled.Should().BeTrue();

        var store = Substitute.For<IInvestigationStore>();
        store.GetAsync(investigation.Id, Arg.Any<CancellationToken>()).Returns(investigation);

        var clock = Substitute.For<IClock>();
        clock.UtcNow.Returns(Now.AddMinutes(1));

        var service = new GeofenceService(store, clock);
        await service.ToggleAsync(investigation.Id, geofence.Id);

        geofence.IsEnabled.Should().BeFalse();
        await store.Received(1).SaveAsync(investigation, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ToggleAsync_EnablesDisabledGeofence()
    {
        var investigation = CreateInvestigationWithGeofence(out var geofence);
        geofence.Disable();
        geofence.IsEnabled.Should().BeFalse();

        var store = Substitute.For<IInvestigationStore>();
        store.GetAsync(investigation.Id, Arg.Any<CancellationToken>()).Returns(investigation);

        var clock = Substitute.For<IClock>();
        clock.UtcNow.Returns(Now.AddMinutes(1));

        var service = new GeofenceService(store, clock);
        await service.ToggleAsync(investigation.Id, geofence.Id);

        geofence.IsEnabled.Should().BeTrue();
    }

    [Fact]
    public async Task RemoveAsync_RemovesGeofenceFromInvestigation()
    {
        var investigation = CreateInvestigationWithGeofence(out var geofence);
        investigation.Geofences.Should().HaveCount(1);

        var store = Substitute.For<IInvestigationStore>();
        store.GetAsync(investigation.Id, Arg.Any<CancellationToken>()).Returns(investigation);

        var clock = Substitute.For<IClock>();
        clock.UtcNow.Returns(Now.AddMinutes(1));

        var service = new GeofenceService(store, clock);
        await service.RemoveAsync(investigation.Id, geofence.Id);

        investigation.Geofences.Should().BeEmpty();
        await store.Received(1).SaveAsync(investigation, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AddFromDrawAsync_CreatesGeofenceOnInvestigation()
    {
        var investigation = Investigation.Create("Draw test", null, Now);

        var store = Substitute.For<IInvestigationStore>();
        store.GetAsync(investigation.Id, Arg.Any<CancellationToken>()).Returns(investigation);

        var clock = Substitute.For<IClock>();
        clock.UtcNow.Returns(Now);

        var service = new GeofenceService(store, clock);
        var dto = await service.AddFromDrawAsync(
            investigation.Id,
            "Test geofence",
            "polygon",
            """{"type":"Polygon","coordinates":[[[-48.0,-27.0],[-48.1,-27.0],[-48.1,-27.1],[-48.0,-27.0]]]}""");

        dto.Should().NotBeNull();
        dto.Name.Should().Be("Test geofence");
        investigation.Geofences.Should().HaveCount(1);
    }
}
