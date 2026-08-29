using Aegis.Application.Dtos;
using Aegis.Domain.Enums;

namespace Aegis.Application.Abstractions;

public interface IDataSourceAdapter
{
    DataSourceType Source { get; }

    string DisplayName { get; }

    bool IsEnabled { get; }

    Task<HealthStatus> ProbeAsync(CancellationToken cancellationToken = default);
}
