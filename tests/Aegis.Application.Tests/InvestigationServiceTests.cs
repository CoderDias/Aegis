using Aegis.Application.Abstractions;
using Aegis.Application.Geo;
using Aegis.Application.Services;
using Aegis.Domain.Entities;
using Aegis.Domain.Enums;
using Aegis.Domain.Exceptions;
using FluentAssertions;
using NSubstitute;

namespace Aegis.Application.Tests;

public class InvestigationServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 26, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task CreateWithGeofenceAsync_CreatesInvestigationAndGeofenceAtomically()
    {
        Investigation? saved = null;
        var store = Substitute.For<IInvestigationStore>();
        store.SaveAsync(Arg.Do<Investigation>(i => saved = i), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var clock = Substitute.For<IClock>();
        clock.UtcNow.Returns(Now);

        var service = new InvestigationService(store, clock);
        var dto = await service.CreateWithGeofenceAsync(
            "SC Investigation",
            "Test desc",
            "polygon",
            """{"type":"Polygon","coordinates":[[[-48.55,-27.59],[-48.56,-27.59],[-48.56,-27.60],[-48.55,-27.59]]]}""");

        dto.Should().NotBeNull();
        dto.Title.Should().Be("SC Investigation");
        saved.Should().NotBeNull();
        saved!.Geofences.Should().HaveCount(1);
        saved.Geofences[0].Name.Should().Be("Área monitorada");
    }

    [Fact]
    public async Task CreateWithGeofenceAsync_ThrowsOnInvalidGeometry()
    {
        var store = Substitute.For<IInvestigationStore>();
        var clock = Substitute.For<IClock>();
        clock.UtcNow.Returns(Now);

        var service = new InvestigationService(store, clock);

        var act = () => service.CreateWithGeofenceAsync(
            "Bad geometry",
            null,
            "polygon",
            """{"type":"Point","coordinates":[-48,-27]}""");

        await act.Should().ThrowAsync<DomainException>();
    }

    [Fact]
    public async Task CreateAsync_CreatesInvestigationWithoutGeofence()
    {
        Investigation? saved = null;
        var store = Substitute.For<IInvestigationStore>();
        store.SaveAsync(Arg.Do<Investigation>(i => saved = i), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var clock = Substitute.For<IClock>();
        clock.UtcNow.Returns(Now);

        var service = new InvestigationService(store, clock);
        var dto = await service.CreateAsync("Simple", null);

        dto.Should().NotBeNull();
        saved.Should().NotBeNull();
        saved!.Geofences.Should().BeEmpty();
    }
}
