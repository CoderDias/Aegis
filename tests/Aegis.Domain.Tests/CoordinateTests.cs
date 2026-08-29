using Aegis.Domain.Exceptions;
using Aegis.Domain.ValueObjects;
using FluentAssertions;

namespace Aegis.Domain.Tests;

public class CoordinateTests
{
    [Fact]
    public void Create_RejectsLatitudeAbove90()
    {
        var act = () => Coordinate.Create(91, 0);

        act.Should().Throw<DomainException>()
            .WithMessage("*Latitude must be between -90 and 90*");
    }

    [Fact]
    public void Create_RejectsLatitudeBelowMinus90()
    {
        var act = () => Coordinate.Create(-91, 0);

        act.Should().Throw<DomainException>()
            .WithMessage("*Latitude must be between -90 and 90*");
    }

    [Fact]
    public void IsNear_ReturnsTrue_WhenPointsAreWithinThreshold()
    {
        var origin = Coordinate.Create(0, 0);
        var nearby = Coordinate.Create(0.0001, 0.0001);

        origin.IsNear(nearby, 50).Should().BeTrue();
    }

    [Fact]
    public void IsNear_ReturnsFalse_WhenPointsAreBeyondThreshold()
    {
        var origin = Coordinate.Create(0, 0);
        var distant = Coordinate.Create(1, 1);

        origin.IsNear(distant, 100).Should().BeFalse();
    }

    [Fact]
    public void IsNear_ReturnsTrue_ForSamePoint()
    {
        var point = Coordinate.Create(-23.5505, -46.6333);

        point.IsNear(point, 0).Should().BeTrue();
    }
}
