using Aegis.Domain.Exceptions;
using Aegis.Domain.ValueObjects;
using FluentAssertions;

namespace Aegis.Domain.Tests;

public class BoundingBoxTests
{
    [Fact]
    public void Contains_IsInclusiveOnAllBorders()
    {
        var bbox = BoundingBox.Create(-10, -20, 10, 20);

        bbox.Contains(Coordinate.Create(-10, -20)).Should().BeTrue();
        bbox.Contains(Coordinate.Create(-10, 20)).Should().BeTrue();
        bbox.Contains(Coordinate.Create(10, -20)).Should().BeTrue();
        bbox.Contains(Coordinate.Create(10, 20)).Should().BeTrue();
        bbox.Contains(Coordinate.Create(0, 0)).Should().BeTrue();
    }

    [Fact]
    public void Contains_ReturnsFalse_OutsideBox()
    {
        var bbox = BoundingBox.Create(-10, -20, 10, 20);

        bbox.Contains(Coordinate.Create(10.0001, 0)).Should().BeFalse();
        bbox.Contains(Coordinate.Create(0, 20.0001)).Should().BeFalse();
    }

    [Fact]
    public void Create_RejectsWestGreaterThanEast()
    {
        var act = () => BoundingBox.Create(-10, 30, 10, 10);

        act.Should().Throw<DomainException>()
            .WithMessage("*West must be less than or equal to East*");
    }

    [Fact]
    public void Create_RejectsSouthGreaterThanOrEqualToNorth()
    {
        var act = () => BoundingBox.Create(10, -20, -10, 20);

        act.Should().Throw<DomainException>()
            .WithMessage("*South must be less than North*");
    }
}
