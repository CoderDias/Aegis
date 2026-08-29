using Aegis.Infrastructure.Data;
using Aegis.Infrastructure.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Aegis.Infrastructure.External.Censys;

public sealed class CensysQuotaService(
    IServiceScopeFactory scopeFactory,
    IOptions<CensysOptions> options)
{
    public async Task<bool> TryConsumeAsync(CancellationToken cancellationToken = default)
    {
        var max = Math.Max(1, options.Value.MaxMonthlyQueries);
        var monthKey = DateTime.UtcNow.ToString("yyyy-MM");

        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AegisDbContext>();
        var usage = await db.CensysApiUsage
            .FirstOrDefaultAsync(x => x.MonthKey == monthKey, cancellationToken)
            .ConfigureAwait(false);

        if (usage is null)
        {
            usage = new CensysApiUsageEntity { MonthKey = monthKey, QueryCount = 0 };
            db.CensysApiUsage.Add(usage);
        }

        if (usage.QueryCount >= max)
        {
            return false;
        }

        usage.QueryCount++;
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }

    public async Task<(int Used, int Max)> GetUsageAsync(CancellationToken cancellationToken = default)
    {
        var max = Math.Max(1, options.Value.MaxMonthlyQueries);
        var monthKey = DateTime.UtcNow.ToString("yyyy-MM");

        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AegisDbContext>();
        var usage = await db.CensysApiUsage
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.MonthKey == monthKey, cancellationToken)
            .ConfigureAwait(false);

        return (usage?.QueryCount ?? 0, max);
    }
}
