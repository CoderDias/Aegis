using Aegis.Application.Abstractions;
using Aegis.Application.Dtos;
using Aegis.Domain.Enums;
using Aegis.Domain.ValueObjects;
using Aegis.Infrastructure.External.OpenSky;
using Aegis.Infrastructure.Options;
using Microsoft.Extensions.Options;

namespace Aegis.Infrastructure.External.Adapters;

public sealed class OpenSkyAdapter(
    OpenSkyClient client,
    IOptions<MapOptions> mapOptions,
    IOptions<OpenSkyOptions> openSkyOptions) : IDataSourceAdapter
{
    public DataSourceType Source => DataSourceType.OpenSky;

    public string DisplayName => "OpenSky Network";

    public bool IsEnabled => openSkyOptions.Value.PollingIntervalSeconds > 0;

    public async Task<HealthStatus> ProbeAsync(CancellationToken cancellationToken = default)
    {
        var started = DateTimeOffset.UtcNow;
        try
        {
            var span = 0.5;
            var bbox = BoundingBox.Create(
                mapOptions.Value.DefaultLat - span,
                mapOptions.Value.DefaultLng - span,
                mapOptions.Value.DefaultLat + span,
                mapOptions.Value.DefaultLng + span);

            _ = await client.GetStatesRawAsync(bbox, cancellationToken).ConfigureAwait(false);
            var latency = DateTimeOffset.UtcNow - started;
            return new HealthStatus(true, "OpenSky reachable.", latency, DateTimeOffset.UtcNow);
        }
        catch (Exception ex)
        {
            return new HealthStatus(false, ex.Message, DateTimeOffset.UtcNow - started, DateTimeOffset.UtcNow);
        }
    }
}
