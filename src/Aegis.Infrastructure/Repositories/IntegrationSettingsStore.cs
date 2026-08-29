using Aegis.Application.Abstractions;
using Aegis.Application.Dtos.Settings;
using Aegis.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Aegis.Infrastructure.Repositories;

public sealed class IntegrationSettingsStore(AegisDbContext db) : IIntegrationSettingsStore
{
    public async Task<IReadOnlyList<IntegrationSettingDto>> ListAsync(CancellationToken cancellationToken = default) =>
        await db.IntegrationSettings
            .AsNoTracking()
            .OrderBy(s => s.SortOrder)
            .Select(s => new IntegrationSettingDto(
                s.Key,
                s.DisplayName,
                s.Enabled,
                false,
                s.SortOrder))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task SetEnabledAsync(string key, bool enabled, CancellationToken cancellationToken = default)
    {
        await db.IntegrationSettings
            .Where(s => s.Key == key)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Enabled, enabled), cancellationToken)
            .ConfigureAwait(false);
    }
}
