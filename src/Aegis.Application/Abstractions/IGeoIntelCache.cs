using Aegis.Application.Dtos.Intel;

namespace Aegis.Application.Abstractions;

public interface IGeoIntelCache
{
    event Action? Updated;

    IReadOnlyList<GeoMarkerDto> GetSeismic();

    IReadOnlyList<GeoMarkerDto> GetShips();

    IReadOnlyList<GeoMarkerDto> GetWeatherAlerts();

    bool IsSeismicStale(TimeSpan minInterval);

    bool IsShipsStale(string bboxKey, TimeSpan minInterval);

    bool IsWeatherAlertsStale(TimeSpan minInterval);

    void SetSeismic(IReadOnlyList<GeoMarkerDto> markers);

    void SetShips(IReadOnlyList<GeoMarkerDto> markers, string bboxKey);

    void SetWeatherAlerts(IReadOnlyList<GeoMarkerDto> markers);
}
