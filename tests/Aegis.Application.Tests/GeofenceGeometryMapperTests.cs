using Aegis.Application.Geo;
using FluentAssertions;

namespace Aegis.Application.Tests;

public class GeofenceGeometryMapperTests
{
    [Fact]
    public void ToGeofenceGeometry_MapsPolygonDrawJson()
    {
        const string drawJson =
            """{"type":"Polygon","coordinates":[[[-47.9,-15.7],[-47.8,-15.7],[-47.8,-15.8],[-47.9,-15.7]]]}""";

        var result = GeofenceGeometryMapper.ToGeofenceGeometry("polygon", drawJson);

        result.Should().Contain("\"type\":\"Polygon\"");
        result.Should().Contain("coordinates");
    }

    [Fact]
    public void ToGeofenceGeometry_MapsCircleDrawJson()
    {
        const string drawJson =
            """{"type":"Point","coordinates":[-47.9,-15.7],"properties":{"radiusMeters":1200}}""";

        var result = GeofenceGeometryMapper.ToGeofenceGeometry("circle", drawJson);

        result.Should().Contain("\"type\":\"Circle\"");
        result.Should().Contain("radiusMeters");
    }
}
