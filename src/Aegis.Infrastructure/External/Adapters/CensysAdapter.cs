using Aegis.Application.Abstractions;
using Aegis.Application.Dtos;
using Aegis.Domain.Enums;
using Aegis.Infrastructure.External.Censys;
using Microsoft.Extensions.Options;

namespace Aegis.Infrastructure.External.Adapters;

public sealed class CensysAdapter(
    CensysClient client,
    CensysQuotaService quota,
    IOptions<CensysOptions> options) : IDataSourceAdapter
{
    public DataSourceType Source => DataSourceType.Censys;
    public string DisplayName => "Censys";
    public bool IsEnabled => options.Value.Enabled && client.IsConfigured;

    public async Task<HealthStatus> ProbeAsync(CancellationToken cancellationToken = default)
    {
        if (!client.IsConfigured)
        {
            return new HealthStatus(false, "ApiToken não configurado", null, DateTimeOffset.UtcNow);
        }

        var (used, max) = await quota.GetUsageAsync(cancellationToken).ConfigureAwait(false);
        var searchNote = client.CanUseSearch
            ? "search API ativo"
            : "free: lookup por IP (search requer OrganizationId)";

        return new HealthStatus(
            true,
            $"{searchNote} · {used}/{max} calls/mês",
            null,
            DateTimeOffset.UtcNow);
    }
}
