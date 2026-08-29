using Aegis.Application.Abstractions;
using Aegis.Application.Dtos;
using Aegis.Domain.Enums;
using Aegis.Infrastructure.External.HostDiscovery;
using Microsoft.Extensions.Options;

namespace Aegis.Infrastructure.External.Adapters;

public sealed class HostDiscoveryAdapter(
    IOptions<HostDiscoveryOptions> options) : IDataSourceAdapter
{
    public DataSourceType Source => DataSourceType.HostDiscovery;
    public string DisplayName => "Descoberta gratuita (CIDR/TCP)";
    public bool IsEnabled => options.Value.Enabled;

    public Task<HealthStatus> ProbeAsync(CancellationToken cancellationToken = default)
    {
        var message = options.Value.Enabled
            ? "InternetDB + ip-api + probe TCP"
            : "Desabilitado";

        return Task.FromResult(new HealthStatus(options.Value.Enabled, message, null, DateTimeOffset.UtcNow));
    }
}
