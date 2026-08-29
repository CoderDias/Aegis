using Aegis.Application.Abstractions;
using Aegis.Application.Dtos;
using Aegis.Domain.Enums;
using Aegis.Infrastructure.External.Overpass;
using Microsoft.Extensions.Options;

namespace Aegis.Infrastructure.External.Adapters;

public sealed class OverpassAdapter(
    OverpassClient client,
    IOptions<OverpassOptions> options) : IDataSourceAdapter
{
    public DataSourceType Source => DataSourceType.Overpass;

    public string DisplayName => "Overpass API";

    public bool IsEnabled => !string.IsNullOrWhiteSpace(options.Value.BaseUrl);

    public async Task<HealthStatus> ProbeAsync(CancellationToken cancellationToken = default)
    {
        var started = DateTimeOffset.UtcNow;
        try
        {
            var ok = await client.ProbeAsync(cancellationToken).ConfigureAwait(false);
            var latency = DateTimeOffset.UtcNow - started;
            return new HealthStatus(
                ok,
                ok ? "Overpass interpreter OK." : "Overpass probe failed.",
                latency,
                DateTimeOffset.UtcNow);
        }
        catch (Exception ex)
        {
            return new HealthStatus(false, ex.Message, DateTimeOffset.UtcNow - started, DateTimeOffset.UtcNow);
        }
    }
}
