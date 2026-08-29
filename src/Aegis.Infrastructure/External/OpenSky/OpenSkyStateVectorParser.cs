using System.Globalization;
using System.Text.Json;
using Aegis.Application.Dtos.Flights;
using Aegis.Domain.Enums;

namespace Aegis.Infrastructure.External.OpenSky;

public sealed record OpenSkyStateVector(
    string Icao24,
    string? Callsign,
    string? OriginCountry,
    DateTimeOffset? TimePosition,
    DateTimeOffset LastContact,
    double? Longitude,
    double? Latitude,
    double? BaroAltitude,
    bool OnGround,
    double? Velocity,
    double? TrueTrack,
    double? VerticalRate,
    double? GeoAltitude);

public static class OpenSkyStateVectorParser
{
    public const int MinStateArrayLength = 17;

    public static IReadOnlyList<OpenSkyStateVector> ParseStatesJson(string json)
    {
        using var document = JsonDocument.Parse(json);
        return ParseStates(document.RootElement);
    }

    public static IReadOnlyList<OpenSkyStateVector> ParseStates(JsonElement root)
    {
        if (!root.TryGetProperty("states", out var statesElement) ||
            statesElement.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var results = new List<OpenSkyStateVector>();

        foreach (var state in statesElement.EnumerateArray())
        {
            if (state.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            var vector = TryParseStateArray(state);
            if (vector is not null)
            {
                results.Add(vector);
            }
        }

        return results;
    }

    public static OpenSkyStateVector? TryParseStateArray(JsonElement stateArray)
    {
        if (stateArray.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var length = stateArray.GetArrayLength();
        if (length < MinStateArrayLength)
        {
            return null;
        }

        var icao24 = GetString(stateArray, 0);
        if (string.IsNullOrWhiteSpace(icao24))
        {
            return null;
        }

        return new OpenSkyStateVector(
            icao24.Trim().ToLowerInvariant(),
            NormalizeCallsign(GetString(stateArray, 1)),
            GetString(stateArray, 2),
            ParseUnixSeconds(GetDouble(stateArray, 3)),
            ParseUnixSeconds(GetDouble(stateArray, 4)) ?? DateTimeOffset.UtcNow,
            GetDouble(stateArray, 5),
            GetDouble(stateArray, 6),
            GetDouble(stateArray, 7),
            GetBool(stateArray, 8),
            GetDouble(stateArray, 9),
            GetDouble(stateArray, 10),
            GetDouble(stateArray, 11),
            GetDouble(stateArray, 13));
    }

    public static AircraftMarkerDto ToMarkerDto(OpenSkyStateVector vector)
    {
        if (vector.Latitude is null || vector.Longitude is null)
        {
            throw new InvalidOperationException($"State vector {vector.Icao24} has no position.");
        }

        return new AircraftMarkerDto(
            vector.Icao24,
            vector.Callsign,
            vector.Latitude.Value,
            vector.Longitude.Value,
            vector.BaroAltitude,
            vector.Velocity,
            vector.TrueTrack,
            vector.OriginCountry,
            vector.OnGround,
            vector.LastContact);
    }

    public static Domain.Entities.FlightTrackPoint ToTrackPoint(
        OpenSkyStateVector vector,
        DateTimeOffset capturedAt,
        DataSourceType source = DataSourceType.OpenSky)
    {
        if (vector.Latitude is null || vector.Longitude is null)
        {
            throw new InvalidOperationException($"State vector {vector.Icao24} has no position.");
        }

        return Domain.Entities.FlightTrackPoint.Create(
            0,
            vector.Icao24,
            vector.TimePosition ?? capturedAt,
            vector.Latitude.Value,
            vector.Longitude.Value,
            source,
            vector.Callsign,
            vector.BaroAltitude,
            vector.GeoAltitude,
            vector.Velocity,
            vector.TrueTrack,
            vector.VerticalRate,
            vector.OriginCountry,
            vector.OnGround);
    }

    public static DateTimeOffset? ParseUnixSeconds(double? seconds)
    {
        if (seconds is null or <= 0)
        {
            return null;
        }

        return DateTimeOffset.FromUnixTimeSeconds((long)seconds.Value);
    }

    private static string? NormalizeCallsign(string? callsign)
    {
        if (string.IsNullOrWhiteSpace(callsign))
        {
            return null;
        }

        return callsign.Trim();
    }

    private static string? GetString(JsonElement array, int index)
    {
        if (index >= array.GetArrayLength())
        {
            return null;
        }

        var element = array[index];
        return element.ValueKind switch
        {
            JsonValueKind.Null or JsonValueKind.Undefined => null,
            JsonValueKind.String => element.GetString(),
            _ => element.GetRawText()
        };
    }

    private static double? GetDouble(JsonElement array, int index)
    {
        if (index >= array.GetArrayLength())
        {
            return null;
        }

        var element = array[index];
        return element.ValueKind switch
        {
            JsonValueKind.Null or JsonValueKind.Undefined => null,
            JsonValueKind.Number => element.GetDouble(),
            JsonValueKind.String when double.TryParse(element.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) => parsed,
            _ => null
        };
    }

    private static bool GetBool(JsonElement array, int index)
    {
        if (index >= array.GetArrayLength())
        {
            return false;
        }

        var element = array[index];
        return element.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Number => element.GetDouble() != 0,
            _ => false
        };
    }
}
