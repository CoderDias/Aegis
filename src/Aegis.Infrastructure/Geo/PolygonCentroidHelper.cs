using System.Text.Json;

namespace Aegis.Infrastructure.Geo;

internal static class PolygonCentroidHelper
{
    public static (double Lat, double Lng)? FromGeoJson(JsonElement geometry)
    {
        if (!geometry.TryGetProperty("type", out var typeElement))
        {
            return null;
        }

        var type = typeElement.GetString();
        if (!geometry.TryGetProperty("coordinates", out var coordinates))
        {
            return null;
        }

        return type switch
        {
            "Polygon" => FromRing(coordinates[0]),
            "MultiPolygon" => FromRing(coordinates[0][0]),
            _ => null
        };
    }

    public static (double Lat, double Lng)? FromCapPolygon(string? polygon)
    {
        if (string.IsNullOrWhiteSpace(polygon))
        {
            return null;
        }

        double latSum = 0;
        double lngSum = 0;
        var count = 0;

        foreach (var token in polygon.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var parts = token.Split(',', StringSplitOptions.TrimEntries);
            if (parts.Length < 2 ||
                !double.TryParse(parts[0], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var lat) ||
                !double.TryParse(parts[1], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var lng))
            {
                continue;
            }

            latSum += lat;
            lngSum += lng;
            count++;
        }

        return count == 0 ? null : (latSum / count, lngSum / count);
    }

    private static (double Lat, double Lng)? FromRing(JsonElement ring)
    {
        double latSum = 0;
        double lngSum = 0;
        var count = 0;

        foreach (var point in ring.EnumerateArray())
        {
            if (point.GetArrayLength() < 2)
            {
                continue;
            }

            lngSum += point[0].GetDouble();
            latSum += point[1].GetDouble();
            count++;
        }

        return count == 0 ? null : (latSum / count, lngSum / count);
    }
}
