using Aegis.Application.Abstractions;
using Aegis.Infrastructure.Geo;

namespace Aegis.Infrastructure.Background;

public sealed class RegionalPrefetchRegistry : IRegionalPrefetchBroker
{
    private readonly object _lock = new();
    private readonly Dictionary<string, RegionalPrefetchStatusDto> _status = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> _queue = [];
    private string? _activeCountry;
    private int _roundRobin;
    private int _cycle;

    public event Action<string>? CountryUpdated;

    public string? ActiveCountryCode
    {
        get
        {
            lock (_lock)
            {
                return _activeCountry;
            }
        }
    }

    public void InitializeCountries(IEnumerable<string> countryCodes)
    {
        lock (_lock)
        {
            foreach (var code in countryCodes.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (_status.ContainsKey(code))
                {
                    continue;
                }

                _status[code] = new RegionalPrefetchStatusDto(
                    code,
                    RegionalPrefetchPhase.Queued,
                    0,
                    0,
                    0,
                    null);
                _queue.Add(code);
            }
        }
    }

    public void SetActiveCountry(string? countryCode)
    {
        lock (_lock)
        {
            _activeCountry = string.IsNullOrWhiteSpace(countryCode)
                ? null
                : countryCode.ToUpperInvariant();
        }
    }

    public RegionalPrefetchStatusDto GetStatus(string countryCode)
    {
        lock (_lock)
        {
            return _status.TryGetValue(countryCode.ToUpperInvariant(), out var status)
                ? status
                : new RegionalPrefetchStatusDto(
                    countryCode.ToUpperInvariant(),
                    RegionalPrefetchPhase.Queued,
                    0,
                    0,
                    0,
                    null);
        }
    }

    public IReadOnlyList<RegionalPrefetchStatusDto> GetAllStatuses()
    {
        lock (_lock)
        {
            return _status.Values.OrderBy(s => s.CountryCode).ToList();
        }
    }

    public void UpdateStatus(RegionalPrefetchStatusDto status)
    {
        lock (_lock)
        {
            _status[status.CountryCode] = status;
        }
    }

    public string? DequeueNextCountry()
    {
        lock (_lock)
        {
            if (_queue.Count == 0)
            {
                return null;
            }

            _cycle++;

            var preferActive = _cycle % 3 != 0 &&
                               !string.IsNullOrEmpty(_activeCountry) &&
                               _status.ContainsKey(_activeCountry);

            if (preferActive)
            {
                return _activeCountry;
            }

            var incomplete = _queue
                .Where(c => _status.TryGetValue(c, out var s) &&
                            s.Phase is RegionalPrefetchPhase.Queued or RegionalPrefetchPhase.Warming)
                .OrderBy(c => c == _activeCountry ? 0 : 1)
                .ThenBy(c => c)
                .FirstOrDefault();

            if (incomplete is not null)
            {
                return incomplete;
            }

            _roundRobin = (_roundRobin + 1) % _queue.Count;
            return _queue[_roundRobin];
        }
    }

    public void NotifyCountryUpdated(string countryCode) =>
        CountryUpdated?.Invoke(countryCode.ToUpperInvariant());

    public RegionalPrefetchSummaryDto GetSummary()
    {
        lock (_lock)
        {
            var all = _status.Values.ToList();
            if (all.Count == 0)
            {
                return new RegionalPrefetchSummaryDto(
                    _activeCountry,
                    RegionalPrefetchPhase.Queued,
                    0,
                    0,
                    0,
                    0,
                    0,
                    0,
                    0);
            }

            var warm = all.Count(s => s.Phase is RegionalPrefetchPhase.Warm or RegionalPrefetchPhase.Refreshing);
            var warming = all.Count(s => s.Phase is RegionalPrefetchPhase.Warming);
            var tilesCached = all.Sum(s => s.OverpassTilesCached);
            var tilesTotal = all.Sum(s => s.OverpassTilesTotal);
            var hosts = all.Sum(s => s.HostsDiscovered);

            var active = !string.IsNullOrEmpty(_activeCountry) &&
                         _status.TryGetValue(_activeCountry, out var activeStatus)
                ? activeStatus
                : null;

            double progress;
            if (tilesTotal > 0)
            {
                progress = Math.Clamp(tilesCached / (double)tilesTotal, 0, 1);
            }
            else if (warm > 0 || warming > 0)
            {
                progress = Math.Clamp((warm + warming * 0.35) / Math.Max(all.Count, 1), 0, 1);
            }
            else
            {
                progress = 0;
            }

            return new RegionalPrefetchSummaryDto(
                _activeCountry,
                active?.Phase ?? RegionalPrefetchPhase.Queued,
                warm,
                warming,
                all.Count,
                tilesCached,
                tilesTotal,
                hosts,
                progress);
        }
    }
}
