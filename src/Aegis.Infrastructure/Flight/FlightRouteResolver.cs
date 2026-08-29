using Aegis.Application.Abstractions;
using Aegis.Application.Dtos.Flights;
using Aegis.Application.Geo;
using Aegis.Infrastructure.External.OpenSky;
using Aegis.Infrastructure.Geo;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Aegis.Infrastructure.Flight;

public sealed class FlightRouteResolver(
    OpenSkyClient openSky,
    AirportCoordinateProvider airports,
    IFlightSnapshotStore snapshotStore,
    IMemoryCache cache,
    ILogger<FlightRouteResolver> logger) : IFlightRouteResolver
{
    public async Task<FlightRouteDto?> ResolveAsync(
        AircraftMarkerDto aircraft,
        CancellationToken cancellationToken = default)
    {
        var flownTrack = await LoadFlownTrackAsync(aircraft, cancellationToken).ConfigureAwait(false);
        IReadOnlyList<string>? airportCodes = null;

        if (!string.IsNullOrWhiteSpace(aircraft.Callsign))
        {
            var callsign = aircraft.Callsign.Trim();
            var cacheKey = $"flight-route:{callsign.ToUpperInvariant()}";
            airportCodes = await cache.GetOrCreateAsync(cacheKey, async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(20);
                try
                {
                    return await openSky.GetRouteAirportsAsync(callsign, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    logger.LogDebug(ex, "OpenSky route lookup failed for {Callsign}", callsign);
                    return null;
                }
            }).ConfigureAwait(false);
        }

        if (airportCodes is { Count: >= 2 })
        {
            var originAirport = await airports.ResolveAsync(airportCodes[0], cancellationToken).ConfigureAwait(false);
            var destAirport = await airports.ResolveAsync(airportCodes[1], cancellationToken).ConfigureAwait(false);

            if (originAirport is not null && destAirport is not null)
            {
                var path = GreatCircleHelper.SampleRoute(
                    originAirport.Value.Lat,
                    originAirport.Value.Lng,
                    destAirport.Value.Lat,
                    destAirport.Value.Lng);

                return new FlightRouteDto(
                    new FlightRouteEndpointDto(airportCodes[0], originAirport.Value.Label, originAirport.Value.Lat, originAirport.Value.Lng),
                    new FlightRouteEndpointDto(airportCodes[1], destAirport.Value.Label, destAirport.Value.Lat, destAirport.Value.Lng),
                    path,
                    flownTrack,
                    IsEstimated: false);
            }
        }

        if (flownTrack is { Count: >= 2 })
        {
            var first = flownTrack[0];
            return new FlightRouteDto(
                new FlightRouteEndpointDto("TRACK", "Início do rastreio", first.Lat, first.Lng),
                null,
                [],
                flownTrack,
                IsEstimated: true);
        }

        if (flownTrack is { Count: 1 })
        {
            var point = flownTrack[0];
            return new FlightRouteDto(
                new FlightRouteEndpointDto("TRACK", "Posição rastreada", point.Lat, point.Lng),
                null,
                [],
                flownTrack,
                IsEstimated: true);
        }

        return null;
    }

    private async Task<IReadOnlyList<FlightRoutePointDto>?> LoadFlownTrackAsync(
        AircraftMarkerDto aircraft,
        CancellationToken cancellationToken)
    {
        var to = DateTimeOffset.UtcNow;
        var from = to.AddHours(-4);

        try
        {
            var points = await snapshotStore
                .GetTrackAsync(aircraft.Icao24, from, to, cancellationToken)
                .ConfigureAwait(false);

            if (points.Count == 0)
            {
                return
                [
                    new FlightRoutePointDto(aircraft.Lat, aircraft.Lng)
                ];
            }

            var track = points
                .Select(p => new FlightRoutePointDto(p.Latitude, p.Longitude))
                .ToList();

            var last = track[^1];
            if (Math.Abs(last.Lat - aircraft.Lat) > 0.001 || Math.Abs(last.Lng - aircraft.Lng) > 0.001)
            {
                track.Add(new FlightRoutePointDto(aircraft.Lat, aircraft.Lng));
            }

            return track;
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Track lookup failed for {Icao24}", aircraft.Icao24);
            return
            [
                new FlightRoutePointDto(aircraft.Lat, aircraft.Lng)
            ];
        }
    }
}
