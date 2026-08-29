using Aegis.Domain.Enums;
using Aegis.Domain.Exceptions;

namespace Aegis.Domain.Entities;

public class FlightTrackPoint
{
    public long Id { get; private set; }
    public string Icao24 { get; private set; } = string.Empty;
    public string? Callsign { get; private set; }
    public DateTimeOffset Time { get; private set; }
    public double Latitude { get; private set; }
    public double Longitude { get; private set; }
    public double? BaroAltitude { get; private set; }
    public double? GeoAltitude { get; private set; }
    public double? Velocity { get; private set; }
    public double? Heading { get; private set; }
    public double? VerticalRate { get; private set; }
    public string? OriginCountry { get; private set; }
    public bool OnGround { get; private set; }
    public DataSourceType Source { get; private set; }

    private FlightTrackPoint()
    {
    }

    public static FlightTrackPoint Create(
        long id,
        string icao24,
        DateTimeOffset time,
        double latitude,
        double longitude,
        DataSourceType source,
        string? callsign = null,
        double? baroAltitude = null,
        double? geoAltitude = null,
        double? velocity = null,
        double? heading = null,
        double? verticalRate = null,
        string? originCountry = null,
        bool onGround = false)
    {
        if (string.IsNullOrWhiteSpace(icao24))
        {
            throw new DomainException("ICAO24 is required.");
        }

        var normalizedIcao24 = icao24.Trim().ToLowerInvariant();
        if (normalizedIcao24.Length != 6)
        {
            throw new DomainException("ICAO24 must be a 6-character hex string.");
        }

        foreach (var c in normalizedIcao24)
        {
            if (!Uri.IsHexDigit(c))
            {
                throw new DomainException("ICAO24 must be a 6-character hex string.");
            }
        }

        return new FlightTrackPoint
        {
            Id = id,
            Icao24 = normalizedIcao24,
            Callsign = string.IsNullOrWhiteSpace(callsign) ? null : callsign.Trim(),
            Time = time,
            Latitude = latitude,
            Longitude = longitude,
            BaroAltitude = baroAltitude,
            GeoAltitude = geoAltitude,
            Velocity = velocity,
            Heading = heading,
            VerticalRate = verticalRate,
            OriginCountry = string.IsNullOrWhiteSpace(originCountry) ? null : originCountry.Trim(),
            OnGround = onGround,
            Source = source
        };
    }
}
