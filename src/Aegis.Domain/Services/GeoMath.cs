using Aegis.Domain.ValueObjects;

namespace Aegis.Domain.Services;

public static class GeoMath
{
    private const double EarthRadiusMeters = 6_371_000d;

    public static double HaversineMeters(Coordinate a, Coordinate b)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);

        var lat1 = DegreesToRadians(a.Latitude);
        var lat2 = DegreesToRadians(b.Latitude);
        var deltaLat = DegreesToRadians(b.Latitude - a.Latitude);
        var deltaLng = DegreesToRadians(b.Longitude - a.Longitude);

        var sinHalfDeltaLat = Math.Sin(deltaLat / 2d);
        var sinHalfDeltaLng = Math.Sin(deltaLng / 2d);
        var haversine = sinHalfDeltaLat * sinHalfDeltaLat +
                        Math.Cos(lat1) * Math.Cos(lat2) * sinHalfDeltaLng * sinHalfDeltaLng;

        return 2d * EarthRadiusMeters * Math.Asin(Math.Min(1d, Math.Sqrt(haversine)));
    }

    public static double InitialBearingDegrees(Coordinate from, Coordinate to)
    {
        ArgumentNullException.ThrowIfNull(from);
        ArgumentNullException.ThrowIfNull(to);

        var lat1 = DegreesToRadians(from.Latitude);
        var lat2 = DegreesToRadians(to.Latitude);
        var deltaLng = DegreesToRadians(to.Longitude - from.Longitude);

        var y = Math.Sin(deltaLng) * Math.Cos(lat2);
        var x = Math.Cos(lat1) * Math.Sin(lat2) -
                Math.Sin(lat1) * Math.Cos(lat2) * Math.Cos(deltaLng);
        var bearingRadians = Math.Atan2(y, x);
        var bearingDegrees = RadiansToDegrees(bearingRadians);

        return (bearingDegrees + 360d) % 360d;
    }

    public static Coordinate DestinationPoint(Coordinate start, double bearingDegrees, double meters)
    {
        ArgumentNullException.ThrowIfNull(start);

        var angularDistance = meters / EarthRadiusMeters;
        var bearing = DegreesToRadians(bearingDegrees);
        var lat1 = DegreesToRadians(start.Latitude);
        var lng1 = DegreesToRadians(start.Longitude);

        var lat2 = Math.Asin(
            Math.Sin(lat1) * Math.Cos(angularDistance) +
            Math.Cos(lat1) * Math.Sin(angularDistance) * Math.Cos(bearing));

        var lng2 = lng1 + Math.Atan2(
            Math.Sin(bearing) * Math.Sin(angularDistance) * Math.Cos(lat1),
            Math.Cos(angularDistance) - Math.Sin(lat1) * Math.Sin(lat2));

        return Coordinate.Create(RadiansToDegrees(lat2), NormalizeLongitude(RadiansToDegrees(lng2)));
    }

    public static bool PointInPolygon(Coordinate point, IReadOnlyList<Coordinate> vertices)
    {
        ArgumentNullException.ThrowIfNull(point);
        ArgumentNullException.ThrowIfNull(vertices);

        if (vertices.Count < 3)
        {
            return false;
        }

        var inside = false;
        for (var i = 0; i < vertices.Count; i++)
        {
            var j = i == 0 ? vertices.Count - 1 : i - 1;
            var vi = vertices[i];
            var vj = vertices[j];

            var intersects = vi.Longitude > point.Longitude != vj.Longitude > point.Longitude &&
                             point.Latitude <
                             (vj.Latitude - vi.Latitude) * (point.Longitude - vi.Longitude) /
                             (vj.Longitude - vi.Longitude) + vi.Latitude;

            if (intersects)
            {
                inside = !inside;
            }
        }

        return inside;
    }

    public static bool PointInCircle(Coordinate point, Coordinate center, double radiusMeters)
    {
        ArgumentNullException.ThrowIfNull(point);
        ArgumentNullException.ThrowIfNull(center);

        if (radiusMeters < 0d)
        {
            throw new ArgumentOutOfRangeException(nameof(radiusMeters), "Radius cannot be negative.");
        }

        return HaversineMeters(point, center) <= radiusMeters;
    }

    private static double DegreesToRadians(double degrees) => degrees * Math.PI / 180d;

    private static double RadiansToDegrees(double radians) => radians * 180d / Math.PI;

    private static double NormalizeLongitude(double longitude)
    {
        while (longitude > 180d)
        {
            longitude -= 360d;
        }

        while (longitude < -180d)
        {
            longitude += 360d;
        }

        return longitude;
    }
}
