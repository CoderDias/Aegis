using System.Text.Json;
using Aegis.Domain.Entities;
using Aegis.Domain.Enums;
using Aegis.Domain.Exceptions;
using Aegis.Domain.ValueObjects;

namespace Aegis.Domain.Services;

public static class GeofenceEvaluator
{
    public static GeofenceTransition EvaluateTransition(
        Coordinate? previous,
        Coordinate current,
        Geofence geofence)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(geofence);

        if (!geofence.IsEnabled)
        {
            return GeofenceTransition.None;
        }

        var wasInside = previous is not null && Contains(geofence, previous);
        var isInside = Contains(geofence, current);

        if (!wasInside && isInside)
        {
            return GeofenceTransition.Entered;
        }

        if (wasInside && !isInside)
        {
            return GeofenceTransition.Exited;
        }

        return GeofenceTransition.None;
    }

    public static bool Contains(Geofence geofence, Coordinate coordinate)
    {
        ArgumentNullException.ThrowIfNull(geofence);
        ArgumentNullException.ThrowIfNull(coordinate);

        if (!geofence.IsEnabled)
        {
            return false;
        }

        using var document = JsonDocument.Parse(geofence.GeometryJson);
        var root = document.RootElement;

        if (root.TryGetProperty("type", out var typeElement))
        {
            var geometryType = typeElement.GetString();
            if (string.Equals(geometryType, "Circle", StringComparison.OrdinalIgnoreCase))
            {
                return ContainsCircle(root, coordinate);
            }

            if (string.Equals(geometryType, "Polygon", StringComparison.OrdinalIgnoreCase))
            {
                return ContainsPolygon(root, coordinate);
            }
        }

        throw new DomainException("Unsupported geofence geometry type.");
    }

    private static bool ContainsCircle(JsonElement root, Coordinate coordinate)
    {
        if (!root.TryGetProperty("center", out var centerElement) ||
            centerElement.ValueKind != JsonValueKind.Array ||
            centerElement.GetArrayLength() < 2)
        {
            throw new DomainException("Circle geofence must define a center as [lng, lat].");
        }

        if (!root.TryGetProperty("radiusMeters", out var radiusElement) ||
            radiusElement.ValueKind != JsonValueKind.Number)
        {
            throw new DomainException("Circle geofence must define radiusMeters.");
        }

        var center = Coordinate.Create(
            centerElement[1].GetDouble(),
            centerElement[0].GetDouble());

        var radiusMeters = radiusElement.GetDouble();
        return GeoMath.PointInCircle(coordinate, center, radiusMeters);
    }

    private static bool ContainsPolygon(JsonElement root, Coordinate coordinate)
    {
        if (!root.TryGetProperty("coordinates", out var coordinatesElement) ||
            coordinatesElement.ValueKind != JsonValueKind.Array ||
            coordinatesElement.GetArrayLength() == 0)
        {
            throw new DomainException("Polygon geofence must define coordinates.");
        }

        var outerRing = coordinatesElement[0];
        if (outerRing.ValueKind != JsonValueKind.Array || outerRing.GetArrayLength() < 3)
        {
            throw new DomainException("Polygon geofence outer ring must contain at least 3 points.");
        }

        var vertices = new List<Coordinate>(outerRing.GetArrayLength());
        foreach (var pointElement in outerRing.EnumerateArray())
        {
            if (pointElement.ValueKind != JsonValueKind.Array || pointElement.GetArrayLength() < 2)
            {
                throw new DomainException("Polygon geofence coordinates must be [lng, lat] pairs.");
            }

            vertices.Add(Coordinate.Create(
                pointElement[1].GetDouble(),
                pointElement[0].GetDouble()));
        }

        return GeoMath.PointInPolygon(coordinate, vertices);
    }
}
