using Aegis.Application.Dtos.Settings;

namespace Aegis.Application.Abstractions;

public interface IIntegrationSettingsStore
{
    Task<IReadOnlyList<IntegrationSettingDto>> ListAsync(CancellationToken cancellationToken = default);

    Task SetEnabledAsync(string key, bool enabled, CancellationToken cancellationToken = default);
}
