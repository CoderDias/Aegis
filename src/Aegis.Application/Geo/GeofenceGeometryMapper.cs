using System.Text.Json;

namespace Aegis.Application.Geo;

public static class GeofenceGeometryMapper
{
    public static string? ToGeofenceGeometry(string drawKind, string? geometryGeoJson)
    {
        if (string.IsNullOrWhiteSpace(geometryGeoJson))
        {
            return null;
        }

        var json = UnwrapJsonString(geometryGeoJson.Trim());
        if (string.IsNullOrWhiteSpace(json) || json[0] is not ('{' or '['))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            var type = root.TryGetProperty("type", out var typeProp) ? typeProp.GetString() : null;

            if (string.Equals(drawKind, "circle", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(type, "Point", StringComparison.OrdinalIgnoreCase) &&
                root.TryGetProperty("coordinates", out var pointCoords) &&
                pointCoords.GetArrayLength() >= 2 &&
                root.TryGetProperty("properties", out var props) &&
                props.TryGetProperty("radiusMeters", out var radiusProp))
            {
                return JsonSerializer.Serialize(new
                {
                    type = "Circle",
                    center = new[] { pointCoords[0].GetDouble(), pointCoords[1].GetDouble() },
                    radiusMeters = radiusProp.GetDouble()
                });
            }

            if (string.Equals(drawKind, "polygon", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(type, "Polygon", StringComparison.OrdinalIgnoreCase) &&
                root.TryGetProperty("coordinates", out var polygonCoords))
            {
                return $$"""{"type":"Polygon","coordinates":{{polygonCoords.GetRawText()}}}""";
            }

            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string UnwrapJsonString(string json)
    {
        if (json.Length < 2 || json[0] != '"')
        {
            return json;
        }

        try
        {
            return JsonSerializer.Deserialize<string>(json) ?? json;
        }
        catch (JsonException)
        {
            return json;
        }
    }
}
