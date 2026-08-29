using Aegis.Application.Dtos.Intel;
using Aegis.Infrastructure.External.Brasil;

namespace Aegis.Infrastructure.External.Weather;

public sealed class WeatherAlertAggregator(
    InmetAlertClient inmet,
    DwdWeatherAlertClient dwd,
    JmaWeatherAlertClient jma,
    RussiaWeatherAlertClient russia)
{
    public async Task<IReadOnlyList<GeoMarkerDto>> FetchActiveAsync(CancellationToken cancellationToken = default)
    {
        var tasks = new[]
        {
            inmet.FetchActiveAsync(cancellationToken),
            dwd.FetchActiveAsync(cancellationToken),
            jma.FetchActiveAsync(cancellationToken),
            russia.FetchActiveAsync(cancellationToken)
        };

        var results = await Task.WhenAll(tasks).ConfigureAwait(false);
        return results.SelectMany(static batch => batch).ToList();
    }
}
