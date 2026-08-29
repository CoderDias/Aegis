using Aegis.Domain.Services;
using Aegis.Domain.ValueObjects;
using FluentAssertions;

namespace Aegis.Domain.Tests;

public class GeoMathTests
{
    [Fact]
    public void HaversineMeters_ReturnsZero_ForSamePoint()
    {
        var point = Coordinate.Create(-23.5505, -46.6333);

        GeoMath.HaversineMeters(point, point).Should().Be(0);
    }

    [Fact]
    public void HaversineMeters_IsApproximately111Km_ForOneDegreeLatitude()
    {
        var start = Coordinate.Create(0, 0);
        var oneDegreeNorth = Coordinate.Create(1, 0);

        var meters = GeoMath.HaversineMeters(start, oneDegreeNorth);

        meters.Should().BeApproximately(111_000, 1_000);
    }
}
