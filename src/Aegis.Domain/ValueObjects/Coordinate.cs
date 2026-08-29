using Aegis.Domain.Exceptions;
using Aegis.Domain.Services;

namespace Aegis.Domain.ValueObjects;

public sealed class Coordinate : IEquatable<Coordinate>
{
    public const double MinLatitude = -90d;
    public const double MaxLatitude = 90d;
    public const double MinLongitude = -180d;
    public const double MaxLongitude = 180d;

    public double Latitude { get; }
    public double Longitude { get; }

    private Coordinate(double latitude, double longitude)
    {
        Latitude = latitude;
        Longitude = longitude;
    }

    public static Coordinate Create(double latitude, double longitude)
    {
        if (latitude is < MinLatitude or > MaxLatitude)
        {
            throw new DomainException(
                $"Latitude must be between {MinLatitude} and {MaxLatitude}. Received {latitude}.");
        }

        if (longitude is < MinLongitude or > MaxLongitude)
        {
            throw new DomainException(
                $"Longitude must be between {MinLongitude} and {MaxLongitude}. Received {longitude}.");
        }

        return new Coordinate(latitude, longitude);
    }

    public bool IsNear(Coordinate other, double meters)
    {
        ArgumentNullException.ThrowIfNull(other);
        return GeoMath.HaversineMeters(this, other) <= meters;
    }

    public double[] ToGeoJsonArray() => [Longitude, Latitude];

    public (double Lat, double Lng) ToLatLng() => (Latitude, Longitude);

    public bool Equals(Coordinate? other)
    {
        if (other is null)
        {
            return false;
        }

        return Latitude.Equals(other.Latitude) && Longitude.Equals(other.Longitude);
    }

    public override bool Equals(object? obj) => obj is Coordinate other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(Latitude, Longitude);

    public static bool operator ==(Coordinate? left, Coordinate? right) =>
        left is null ? right is null : left.Equals(right);

    public static bool operator !=(Coordinate? left, Coordinate? right) => !(left == right);

    public override string ToString() => $"({Latitude}, {Longitude})";
}
