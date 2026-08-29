using Aegis.Application.Abstractions;
using Aegis.Application.Dtos;
using Aegis.Domain.Enums;
using Aegis.Infrastructure.External.AirStream;
using Aegis.Infrastructure.External.Shodan;
using Microsoft.Extensions.Options;

namespace Aegis.Infrastructure.External.Adapters;

public sealed class ShodanAdapter(
    IShodanDeviceService client,
    IOptions<ShodanOptions> options) : IDataSourceAdapter
{
    public DataSourceType Source => DataSourceType.Shodan;
    public string DisplayName => "Shodan";
    public bool IsEnabled => options.Value.Enabled && client.IsConfigured;

    public async Task<HealthStatus> ProbeAsync(CancellationToken cancellationToken = default)
    {
        if (!client.IsConfigured)
        {
            return new HealthStatus(false, "ApiKey não configurada", null, DateTimeOffset.UtcNow);
        }

        var info = await client.GetApiInfoAsync(cancellationToken).ConfigureAwait(false);
        if (info is null)
        {
            return new HealthStatus(false, "Indisponível", null, DateTimeOffset.UtcNow);
        }

        var message = info.SearchAvailable
            ? $"Online ({info.Plan}, {info.QueryCredits} query credits)"
            : $"Plano {info.Plan} — busca no mapa requer Membership ({info.QueryCredits} query credits)";

        return new HealthStatus(true, message, null, DateTimeOffset.UtcNow);
    }
}

public sealed class AirStreamAdapter(
    IOptions<AirStreamOptions> options) : IDataSourceAdapter
{
    public DataSourceType Source => DataSourceType.AirStream;
    public string DisplayName => "AirStream (adsb.fi)";
    public bool IsEnabled => options.Value.Enabled;

    public Task<HealthStatus> ProbeAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(new HealthStatus(
            options.Value.Enabled,
            options.Value.Enabled ? "Configurado" : "Desabilitado",
            null,
            DateTimeOffset.UtcNow));
}
