using Aegis.Application.Dtos.Flights;
using Aegis.Application.Dtos.Geo;

namespace Aegis.Application.Geo;

public static class GreatCircleHelper
{
    public static IReadOnlyList<FlightRoutePointDto> SampleRoute(
        double lat1,
        double lon1,
        double lat2,
        double lon2,
        int segments = 48)
    {
        segments = Math.Clamp(segments, 2, 128);
        var points = new List<FlightRoutePointDto>(segments + 1);

        var phi1 = ToRadians(lat1);
        var lambda1 = ToRadians(lon1);
        var phi2 = ToRadians(lat2);
        var lambda2 = ToRadians(lon2);

        var delta = 2 * Math.Asin(Math.Sqrt(
            Math.Pow(Math.Sin((phi2 - phi1) / 2), 2) +
            Math.Cos(phi1) * Math.Cos(phi2) * Math.Pow(Math.Sin((lambda2 - lambda1) / 2), 2)));

        for (var i = 0; i <= segments; i++)
        {
            var fraction = (double)i / segments;
            if (delta < 1e-10)
            {
                points.Add(new FlightRoutePointDto(lat1, lon1));
                continue;
            }

            var a = Math.Sin((1 - fraction) * delta) / Math.Sin(delta);
            var b = Math.Sin(fraction * delta) / Math.Sin(delta);
            var x = a * Math.Cos(phi1) * Math.Cos(lambda1) + b * Math.Cos(phi2) * Math.Cos(lambda2);
            var y = a * Math.Cos(phi1) * Math.Sin(lambda1) + b * Math.Cos(phi2) * Math.Sin(lambda2);
            var z = a * Math.Sin(phi1) + b * Math.Sin(phi2);
            var phi = Math.Atan2(z, Math.Sqrt(x * x + y * y));
            var lambda = Math.Atan2(y, x);
            points.Add(new FlightRoutePointDto(ToDegrees(phi), ToDegrees(lambda)));
        }

        return points;
    }

    private static double ToRadians(double degrees) => degrees * Math.PI / 180d;

    private static double ToDegrees(double radians) => radians * 180d / Math.PI;
}
