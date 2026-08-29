using Aegis.Domain.Exceptions;

namespace Aegis.Domain.ValueObjects;

public sealed class BoundingBox : IEquatable<BoundingBox>
{
    public double South { get; }
    public double West { get; }
    public double North { get; }
    public double East { get; }

    private BoundingBox(double south, double west, double north, double east)
    {
        South = south;
        West = west;
        North = north;
        East = east;
    }

    public static BoundingBox Create(double south, double west, double north, double east)
    {
        if (south >= north)
        {
            throw new DomainException("South must be less than North.");
        }

        if (west > east)
        {
            throw new DomainException(
                "West must be less than or equal to East. Dateline wrap is not supported in v1.");
        }

        if (south is < Coordinate.MinLatitude or > Coordinate.MaxLatitude ||
            north is < Coordinate.MinLatitude or > Coordinate.MaxLatitude)
        {
            throw new DomainException("Latitude values must be between -90 and 90.");
        }

        if (west is < Coordinate.MinLongitude or > Coordinate.MaxLongitude ||
            east is < Coordinate.MinLongitude or > Coordinate.MaxLongitude)
        {
            throw new DomainException("Longitude values must be between -180 and 180.");
        }

        return new BoundingBox(south, west, north, east);
    }

    public bool Contains(Coordinate coordinate)
    {
        ArgumentNullException.ThrowIfNull(coordinate);
        return coordinate.Latitude >= South &&
               coordinate.Latitude <= North &&
               coordinate.Longitude >= West &&
               coordinate.Longitude <= East;
    }

    public bool Intersects(BoundingBox other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return South <= other.North &&
               North >= other.South &&
               West <= other.East &&
               East >= other.West;
    }

    public (double Lamin, double Lomin, double Lamax, double Lomax) ToOpenSkyParams() =>
        (South, West, North, East);

    public (double South, double West, double North, double East) ToOverpassBbox() =>
        (South, West, North, East);

    public bool Equals(BoundingBox? other)
    {
        if (other is null)
        {
            return false;
        }

        return South.Equals(other.South) &&
               West.Equals(other.West) &&
               North.Equals(other.North) &&
               East.Equals(other.East);
    }

    public override bool Equals(object? obj) => obj is BoundingBox other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(South, West, North, East);

    public static bool operator ==(BoundingBox? left, BoundingBox? right) =>
        left is null ? right is null : left.Equals(right);

    public static bool operator !=(BoundingBox? left, BoundingBox? right) => !(left == right);

    public override string ToString() => $"S={South}, W={West}, N={North}, E={East}";
}
