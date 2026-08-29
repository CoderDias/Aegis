using Aegis.Application.Abstractions;
using Aegis.Application.Dtos;
using Aegis.Domain.Enums;
using Aegis.Infrastructure.External.Nominatim;
using Microsoft.Extensions.Options;

namespace Aegis.Infrastructure.External.Adapters;

public sealed class NominatimAdapter(
    NominatimClient client,
    IOptions<NominatimOptions> options) : IDataSourceAdapter
{
    public DataSourceType Source => DataSourceType.Nominatim;

    public string DisplayName => "Nominatim";

    public bool IsEnabled => !string.IsNullOrWhiteSpace(options.Value.BaseUrl);

    public async Task<HealthStatus> ProbeAsync(CancellationToken cancellationToken = default)
    {
        var started = DateTimeOffset.UtcNow;
        try
        {
            var results = await client.SearchAsync("Brasilia", 1, cancellationToken).ConfigureAwait(false);
            var latency = DateTimeOffset.UtcNow - started;
            var healthy = results.Count > 0;
            return new HealthStatus(
                healthy,
                healthy ? "Nominatim search OK." : "Nominatim returned no results.",
                latency,
                DateTimeOffset.UtcNow);
        }
        catch (Exception ex)
        {
            return new HealthStatus(false, ex.Message, DateTimeOffset.UtcNow - started, DateTimeOffset.UtcNow);
        }
    }
}
