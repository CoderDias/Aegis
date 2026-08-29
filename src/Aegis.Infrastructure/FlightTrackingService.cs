using Aegis.Application.Abstractions;
using Aegis.Application.Dtos.Flights;
using Aegis.Application.Dtos.Geo;
using Aegis.Application.Mapping;
using Aegis.Infrastructure.Options;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace Aegis.Infrastructure;

public sealed class FlightTrackingService(
    IMemoryCache memoryCache,
    IOptions<FlightsOptions> flightsOptions) : IFlightTrackingService
{
    public const string CacheKey = "flights:current";

    public Task<IReadOnlyList<AircraftMarkerDto>> GetCurrentInViewportAsync(
        BoundingBoxDto bbox,
        FlightFilterDto filters,
        CancellationToken cancellationToken = default)
    {
        if (!memoryCache.TryGetValue(CacheKey, out CachedFlightSnapshot? snapshot) || snapshot is null)
        {
            return Task.FromResult<IReadOnlyList<AircraftMarkerDto>>([]);
        }

        var domainBox = bbox.ToDomain();
        var filtered = snapshot.Aircraft
            .Where(a => domainBox.Contains(Aegis.Domain.ValueObjects.Coordinate.Create(a.Lat, a.Lng)))
            .Where(a => MatchesFilters(a, filters))
            .Take(flightsOptions.Value.MaxMarkers)
            .ToList();

        return Task.FromResult<IReadOnlyList<AircraftMarkerDto>>(filtered);
    }

    public Task<AircraftMarkerDto?> GetByIcaoAsync(string icao24, CancellationToken cancellationToken = default)
    {
        if (!memoryCache.TryGetValue(CacheKey, out CachedFlightSnapshot? snapshot) || snapshot is null)
        {
            return Task.FromResult<AircraftMarkerDto?>(null);
        }

        var normalized = icao24.Trim().ToLowerInvariant();
        var match = snapshot.Aircraft.FirstOrDefault(a =>
            string.Equals(a.Icao24, normalized, StringComparison.OrdinalIgnoreCase));
        return Task.FromResult(match);
    }

    private static bool MatchesFilters(AircraftMarkerDto aircraft, FlightFilterDto filters)
    {
        if (filters.MinAltitude is not null &&
            (aircraft.BaroAltitude is null || aircraft.BaroAltitude < filters.MinAltitude))
        {
            return false;
        }

        if (filters.MaxAltitude is not null &&
            (aircraft.BaroAltitude is null || aircraft.BaroAltitude > filters.MaxAltitude))
        {
            return false;
        }

        if (filters.MinVelocity is not null &&
            (aircraft.Velocity is null || aircraft.Velocity < filters.MinVelocity))
        {
            return false;
        }

        if (filters.MaxVelocity is not null &&
            (aircraft.Velocity is null || aircraft.Velocity > filters.MaxVelocity))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(filters.CallsignContains))
        {
            if (aircraft.Callsign is null ||
                !aircraft.Callsign.Contains(filters.CallsignContains, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        if (filters.OnGround is not null && aircraft.OnGround != filters.OnGround)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(filters.OriginCountry) &&
            !string.Equals(aircraft.OriginCountry, filters.OriginCountry, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return true;
    }
}

public sealed record CachedFlightSnapshot(
    DateTimeOffset CapturedAt,
    BoundingBoxDto Bbox,
    IReadOnlyList<AircraftMarkerDto> Aircraft);
