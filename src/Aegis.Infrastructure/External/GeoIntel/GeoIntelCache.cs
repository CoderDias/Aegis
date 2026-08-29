using Aegis.Application.Abstractions;
using Aegis.Application.Dtos.Intel;
using Aegis.Application.Geo;

namespace Aegis.Infrastructure.External.GeoIntel;

public sealed class GeoIntelCache : IGeoIntelCache
{
    private readonly object _lock = new();
    private IReadOnlyList<GeoMarkerDto> _seismic = [];
    private IReadOnlyList<GeoMarkerDto> _ships = [];
    private IReadOnlyList<GeoMarkerDto> _weatherAlerts = [];
    private DateTimeOffset? _seismicUpdatedAt;
    private DateTimeOffset? _shipsUpdatedAt;
    private DateTimeOffset? _weatherAlertsUpdatedAt;
    private string? _shipsBboxKey;

    public event Action? Updated;

    public IReadOnlyList<GeoMarkerDto> GetSeismic()
    {
        lock (_lock)
        {
            return _seismic;
        }
    }

    public IReadOnlyList<GeoMarkerDto> GetShips()
    {
        lock (_lock)
        {
            return _ships;
        }
    }

    public IReadOnlyList<GeoMarkerDto> GetWeatherAlerts()
    {
        lock (_lock)
        {
            return _weatherAlerts;
        }
    }

    public bool IsSeismicStale(TimeSpan minInterval)
    {
        lock (_lock)
        {
            return _seismicUpdatedAt is null ||
                   DateTimeOffset.UtcNow - _seismicUpdatedAt.Value >= minInterval;
        }
    }

    public bool IsShipsStale(string bboxKey, TimeSpan minInterval)
    {
        lock (_lock)
        {
            return _shipsUpdatedAt is null ||
                   !string.Equals(_shipsBboxKey, bboxKey, StringComparison.Ordinal) ||
                   DateTimeOffset.UtcNow - _shipsUpdatedAt.Value >= minInterval;
        }
    }

    public bool IsWeatherAlertsStale(TimeSpan minInterval)
    {
        lock (_lock)
        {
            return _weatherAlertsUpdatedAt is null ||
                   DateTimeOffset.UtcNow - _weatherAlertsUpdatedAt.Value >= minInterval;
        }
    }

    public void SetSeismic(IReadOnlyList<GeoMarkerDto> markers)
    {
        if (markers.Count == 0)
        {
            return;
        }

        lock (_lock)
        {
            var merged = _seismic.ToDictionary(m => m.Id, StringComparer.OrdinalIgnoreCase);
            foreach (var marker in markers)
            {
                merged[marker.Id] = marker;
            }

            var now = DateTimeOffset.UtcNow;
            _seismic = merged.Values
                .Where(m => SeismicDisplayPolicy.KeepInCache(m, now))
                .OrderByDescending(m => m.Timestamp ?? DateTimeOffset.MinValue)
                .ToList();
            _seismicUpdatedAt = now;
        }

        Updated?.Invoke();
    }

    public void SetShips(IReadOnlyList<GeoMarkerDto> markers, string bboxKey)
    {
        lock (_lock)
        {
            if (markers.Count == 0 && _ships.Count > 0 &&
                string.Equals(_shipsBboxKey, bboxKey, StringComparison.Ordinal))
            {
                return;
            }

            _ships = markers;
            _shipsBboxKey = bboxKey;
            _shipsUpdatedAt = DateTimeOffset.UtcNow;
        }

        Updated?.Invoke();
    }

    public void SetWeatherAlerts(IReadOnlyList<GeoMarkerDto> markers)
    {
        lock (_lock)
        {
            _weatherAlerts = markers;
            _weatherAlertsUpdatedAt = DateTimeOffset.UtcNow;
        }

        Updated?.Invoke();
    }
}
