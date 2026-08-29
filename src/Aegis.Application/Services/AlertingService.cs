using Aegis.Application.Abstractions;
using Aegis.Application.Dtos.Flights;
using Aegis.Application.Dtos.Intel;
using Aegis.Domain.Services;
using Aegis.Domain.ValueObjects;

namespace Aegis.Application.Services;

public record GeofenceAlertResult(
    Guid GeofenceId,
    string GeofenceName,
    string Message,
    string Category);

/// <summary>
/// Avalia geofences da investigação ativa contra voos, notícias e hosts Shodan.
/// </summary>
public sealed class AlertingService(IInvestigationStore store, IClock clock)
{
    private static readonly TimeSpan DebounceInterval = TimeSpan.FromSeconds(60);

    private readonly Dictionary<Guid, HashSet<string>> _aircraftInside = new();
    private readonly Dictionary<Guid, HashSet<string>> _newsInside = new();
    private readonly Dictionary<Guid, HashSet<string>> _shodanInside = new();
    private readonly Dictionary<string, DateTimeOffset> _lastAlertUtc = new(StringComparer.OrdinalIgnoreCase);

    private readonly SemaphoreSlim _evaluateGate = new(1, 1);

    public Task<IReadOnlyList<GeofenceAlertResult>> EvaluateAsync(
        Guid investigationId,
        IReadOnlyList<AircraftMarkerDto> aircraft,
        CancellationToken cancellationToken = default) =>
        EvaluateAsync(investigationId, aircraft, [], [], cancellationToken);

    public async Task<IReadOnlyList<GeofenceAlertResult>> EvaluateAsync(
        Guid investigationId,
        IReadOnlyList<AircraftMarkerDto> aircraft,
        IReadOnlyList<NewsItemDto> news,
        IReadOnlyList<ShodanHostDto> shodanHosts,
        CancellationToken cancellationToken = default)
    {
        await _evaluateGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var investigation = await store.GetAsync(investigationId, cancellationToken).ConfigureAwait(false);
            if (investigation is null)
            {
                return [];
            }

            var enabledGeofences = investigation.Geofences.Where(g => g.IsEnabled).ToList();
            if (enabledGeofences.Count == 0)
            {
                return [];
            }

            var alerts = new List<GeofenceAlertResult>();
            var now = clock.UtcNow;
            var dirty = false;

            foreach (var geofence in enabledGeofences)
            {
                EvaluateAircraft(geofence, aircraft, investigation, now, alerts, ref dirty);
                EvaluateNews(geofence, news, investigation, now, alerts, ref dirty);
                EvaluateShodan(geofence, shodanHosts, investigation, now, alerts, ref dirty);
            }

            if (dirty)
            {
                await store.SaveAsync(investigation, cancellationToken).ConfigureAwait(false);
            }

            return alerts;
        }
        finally
        {
            _evaluateGate.Release();
        }
    }

    private void EvaluateAircraft(
        Domain.Entities.Geofence geofence,
        IReadOnlyList<AircraftMarkerDto> aircraft,
        Domain.Entities.Investigation investigation,
        DateTimeOffset now,
        List<GeofenceAlertResult> alerts,
        ref bool dirty)
    {
        _aircraftInside.TryGetValue(geofence.Id, out var previouslyInside);
        previouslyInside ??= [];
        var insideNow = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var marker in aircraft)
        {
            var coordinate = Coordinate.Create(marker.Lat, marker.Lng);
            if (!GeofenceEvaluator.Contains(geofence, coordinate))
            {
                continue;
            }

            insideNow.Add(marker.Icao24);

            if (previouslyInside.Contains(marker.Icao24))
            {
                continue;
            }

            var key = $"aircraft:enter:{geofence.Id}:{marker.Icao24}";
            if (IsDebounced(key, now))
            {
                continue;
            }

            var label = string.IsNullOrWhiteSpace(marker.Callsign) ? marker.Icao24 : marker.Callsign.Trim();
            var message = $"Aeronave {label} entrou na área \"{geofence.Name}\".";
            PersistAlert(investigation, geofence, marker.Icao24, null, message, now, ref dirty);
            alerts.Add(new GeofenceAlertResult(geofence.Id, geofence.Name, message, "aircraft"));
        }

        foreach (var icao in previouslyInside.Where(id => !insideNow.Contains(id)))
        {
            var key = $"aircraft:exit:{geofence.Id}:{icao}";
            if (IsDebounced(key, now))
            {
                continue;
            }

            var message = $"Aeronave {icao} saiu da área \"{geofence.Name}\".";
            PersistAlert(investigation, geofence, icao, null, message, now, ref dirty);
            alerts.Add(new GeofenceAlertResult(geofence.Id, geofence.Name, message, "aircraft"));
        }

        _aircraftInside[geofence.Id] = insideNow;
    }

    private void EvaluateNews(
        Domain.Entities.Geofence geofence,
        IReadOnlyList<NewsItemDto> news,
        Domain.Entities.Investigation investigation,
        DateTimeOffset now,
        List<GeofenceAlertResult> alerts,
        ref bool dirty)
    {
        _newsInside.TryGetValue(geofence.Id, out var previouslyInside);
        previouslyInside ??= [];
        var insideNow = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var item in news.Where(n => n.Lat is not null && n.Lng is not null))
        {
            var coordinate = Coordinate.Create(item.Lat!.Value, item.Lng!.Value);
            if (!GeofenceEvaluator.Contains(geofence, coordinate))
            {
                continue;
            }

            var newsId = item.Id.ToString();
            insideNow.Add(newsId);

            if (previouslyInside.Contains(newsId))
            {
                continue;
            }

            var key = $"news:enter:{geofence.Id}:{newsId}";
            if (IsDebounced(key, now))
            {
                continue;
            }

            var message = $"Notícia na área \"{geofence.Name}\": {item.Title}";
            PersistAlert(investigation, geofence, newsId, item.Title, message, now, ref dirty);
            alerts.Add(new GeofenceAlertResult(geofence.Id, geofence.Name, message, "news"));
        }

        _newsInside[geofence.Id] = insideNow;
    }

    private void EvaluateShodan(
        Domain.Entities.Geofence geofence,
        IReadOnlyList<ShodanHostDto> shodanHosts,
        Domain.Entities.Investigation investigation,
        DateTimeOffset now,
        List<GeofenceAlertResult> alerts,
        ref bool dirty)
    {
        _shodanInside.TryGetValue(geofence.Id, out var previouslyInside);
        previouslyInside ??= [];
        var insideNow = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var host in shodanHosts)
        {
            var coordinate = Coordinate.Create(host.Lat, host.Lng);
            if (!GeofenceEvaluator.Contains(geofence, coordinate))
            {
                continue;
            }

            insideNow.Add(host.Ip);

            if (previouslyInside.Contains(host.Ip))
            {
                continue;
            }

            var key = $"shodan:enter:{geofence.Id}:{host.Ip}";
            if (IsDebounced(key, now))
            {
                continue;
            }

            var message = $"Dispositivo IoT {host.Ip} detectado na área \"{geofence.Name}\".";
            PersistAlert(investigation, geofence, host.Ip, host.Org, message, now, ref dirty);
            alerts.Add(new GeofenceAlertResult(geofence.Id, geofence.Name, message, "shodan"));
        }

        foreach (var ip in previouslyInside.Where(id => !insideNow.Contains(id)))
        {
            var key = $"shodan:exit:{geofence.Id}:{ip}";
            if (IsDebounced(key, now))
            {
                continue;
            }

            var message = $"Dispositivo IoT {ip} desconectado da área \"{geofence.Name}\".";
            PersistAlert(investigation, geofence, ip, null, message, now, ref dirty);
            alerts.Add(new GeofenceAlertResult(geofence.Id, geofence.Name, message, "shodan"));
        }

        _shodanInside[geofence.Id] = insideNow;
    }

    private bool IsDebounced(string key, DateTimeOffset now)
    {
        if (_lastAlertUtc.TryGetValue(key, out var last) && now - last < DebounceInterval)
        {
            return true;
        }

        _lastAlertUtc[key] = now;
        return false;
    }

    private static void PersistAlert(
        Domain.Entities.Investigation investigation,
        Domain.Entities.Geofence geofence,
        string entityId,
        string? subtitle,
        string message,
        DateTimeOffset now,
        ref bool dirty)
    {
        investigation.RecordGeofenceAlert(geofence, entityId, subtitle, message, now);
        dirty = true;
    }
}
