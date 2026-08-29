using Aegis.Domain.Entities;
using Aegis.Domain.Enums;
using Aegis.Domain.Exceptions;
using Aegis.Domain.ValueObjects;
using FluentAssertions;

namespace Aegis.Domain.Tests;

public class InvestigationTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 25, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void AddAsset_Throws_WhenInvestigationIsArchived()
    {
        var investigation = Investigation.Create("Archived case", null, Now);
        investigation.ChangeStatus(InvestigationStatus.Archived, Now);

        var asset = CreateAircraftAsset(investigation.Id, "abc123", "ABC123");

        var act = () => investigation.AddAsset(asset, Now);

        act.Should().Throw<DomainException>()
            .WithMessage("*Archived investigations are read-only*");
    }

    [Fact]
    public void AddAsset_Throws_WhenDuplicateExternalKey()
    {
        var investigation = Investigation.Create("Duplicate asset case", null, Now);
        var first = CreateAircraftAsset(investigation.Id, "abc123", "ABC123");
        var duplicate = CreateAircraftAsset(investigation.Id, "ABC123", "Other name");

        investigation.AddAsset(first, Now);

        var act = () => investigation.AddAsset(duplicate, Now);

        act.Should().Throw<DomainException>()
            .WithMessage("*same natural key already exists*");
    }

    [Fact]
    public void AddGeofence_Throws_WhenMaxGeofencesExceeded()
    {
        var investigation = Investigation.Create("Geofence limits", null, Now);

        for (var i = 0; i < Investigation.MaxGeofences; i++)
        {
            var geofence = Geofence.Create(
                Guid.NewGuid(),
                investigation.Id,
                $"Fence {i}",
                """{"type":"Circle","center":[-46.63,-23.55],"radiusMeters":1000}""",
                Now);
            investigation.AddGeofence(geofence, Now);
        }

        var overflow = Geofence.Create(
            Guid.NewGuid(),
            investigation.Id,
            "Overflow",
            """{"type":"Circle","center":[-46.63,-23.55],"radiusMeters":1000}""",
            Now);

        var act = () => investigation.AddGeofence(overflow, Now);

        act.Should().Throw<DomainException>()
            .WithMessage($"*cannot have more than {Investigation.MaxGeofences} geofences*");
    }

    [Fact]
    public void AddAsset_Throws_WhenMaxAssetsExceeded()
    {
        var investigation = Investigation.Create("Asset limits", null, Now);

        for (var i = 0; i < Investigation.MaxAssets; i++)
        {
            var asset = CreateAircraftAsset(investigation.Id, $"icao{i:D4}", $"Aircraft {i}");
            investigation.AddAsset(asset, Now);
        }

        var overflow = CreateAircraftAsset(investigation.Id, "overflow", "Overflow");

        var act = () => investigation.AddAsset(overflow, Now);

        act.Should().Throw<DomainException>()
            .WithMessage($"*cannot have more than {Investigation.MaxAssets} assets*");
    }

    private static Asset CreateAircraftAsset(Guid investigationId, string icao24, string displayName) =>
        Asset.Create(
            Guid.NewGuid(),
            investigationId,
            AssetType.Aircraft,
            displayName,
            DataSourceType.OpenSky,
            icao24,
            Coordinate.Create(-23.55, -46.63),
            "{}",
            Now);
}
