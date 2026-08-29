using Aegis.Infrastructure.External.OpenSky;
using FluentAssertions;

namespace Aegis.Infrastructure.Tests;

public class OpenSkyStateVectorParserTests
{
    private static string FixturePath =>
        Path.Combine(AppContext.BaseDirectory, "Fixtures", "opensky-states.json");

    [Fact]
    public void ParseStatesJson_ReturnsTwoPositionedAircraft_DiscardingNullLatitude()
    {
        var json = File.ReadAllText(FixturePath);

        var vectors = OpenSkyStateVectorParser.ParseStatesJson(json);

        vectors.Should().HaveCount(3);

        var positioned = vectors
            .Where(v => v.Latitude is not null && v.Longitude is not null)
            .ToList();

        positioned.Should().HaveCount(2);
        positioned.Should().Contain(v => v.Icao24 == "abc123" && v.OnGround == false);
        positioned.Should().Contain(v => v.Icao24 == "def456" && v.OnGround == true);

        vectors.Single(v => v.Icao24 == "bad000").Latitude.Should().BeNull();
    }

    [Fact]
    public void ToMarkerDto_Throws_ForStateWithoutPosition()
    {
        var json = File.ReadAllText(FixturePath);
        var nullLatVector = OpenSkyStateVectorParser.ParseStatesJson(json)
            .Single(v => v.Icao24 == "bad000");

        var act = () => OpenSkyStateVectorParser.ToMarkerDto(nullLatVector);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*has no position*");
    }

    [Fact]
    public void ToMarkerDto_MapsPositionedStates()
    {
        var json = File.ReadAllText(FixturePath);
        var vectors = OpenSkyStateVectorParser.ParseStatesJson(json)
            .Where(v => v.Latitude is not null && v.Longitude is not null)
            .ToList();

        var markers = vectors.Select(OpenSkyStateVectorParser.ToMarkerDto).ToList();

        markers.Should().HaveCount(2);
        markers.Should().Contain(m => m.Icao24 == "abc123" && m.Lat == -23.5505 && m.Lng == -46.6333);
        markers.Should().Contain(m => m.Icao24 == "def456" && m.OnGround);
    }
}
