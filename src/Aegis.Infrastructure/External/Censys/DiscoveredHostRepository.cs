using Aegis.Infrastructure.Data;
using Aegis.Infrastructure.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Aegis.Infrastructure.External.Censys;

public sealed class DiscoveredHostRepository(IServiceScopeFactory scopeFactory)
{
    public async Task<DiscoveredHostEntity?> FindAsync(string ip, CancellationToken cancellationToken = default)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AegisDbContext>();
        return await db.DiscoveredHosts
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Ip == ip, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<bool> ExistsAsync(string ip, CancellationToken cancellationToken = default)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AegisDbContext>();
        return await db.DiscoveredHosts.AnyAsync(x => x.Ip == ip, cancellationToken).ConfigureAwait(false);
    }

    public async Task UpsertAsync(DiscoveredHostEntity entity, CancellationToken cancellationToken = default)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AegisDbContext>();
        var existing = await db.DiscoveredHosts
            .FirstOrDefaultAsync(x => x.Ip == entity.Ip, cancellationToken)
            .ConfigureAwait(false);

        var now = DateTimeOffset.UtcNow;
        if (existing is null)
        {
            entity.CreatedAt = now;
            entity.UpdatedAt = now;
            db.DiscoveredHosts.Add(entity);
        }
        else
        {
            existing.CountryCode = entity.CountryCode;
            existing.Lat ??= entity.Lat;
            existing.Lng ??= entity.Lng;
            existing.City ??= entity.City;
            existing.Country ??= entity.Country;
            existing.Org ??= entity.Org;
            existing.Product ??= entity.Product;
            existing.Port ??= entity.Port;
            existing.Transport ??= entity.Transport;
            existing.Source = entity.Source;
            existing.IsUp = entity.IsUp;
            existing.LastProbeAt = entity.LastProbeAt ?? existing.LastProbeAt;
            existing.CensysFetchedAt = entity.CensysFetchedAt ?? existing.CensysFetchedAt;
            if (!string.IsNullOrWhiteSpace(entity.VulnerabilitiesJson))
            {
                existing.VulnerabilitiesJson = entity.VulnerabilitiesJson;
            }

            existing.UpdatedAt = now;
        }

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<DiscoveredHostEntity>> ListInViewportAsync(
        string countryCode,
        double south,
        double north,
        double west,
        double east,
        CancellationToken cancellationToken = default)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AegisDbContext>();
        return await db.DiscoveredHosts
            .AsNoTracking()
            .Where(x =>
                x.Lat != null &&
                x.Lng != null &&
                x.Lat >= south &&
                x.Lat <= north &&
                x.Lng >= west &&
                x.Lng <= east &&
                x.IsUp != false)
            .OrderBy(x => x.Ip)
            .Take(500)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<DiscoveredHostEntity>> ListByCountryAsync(
        string countryCode,
        int limit = 500,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(countryCode))
        {
            return [];
        }

        var code = countryCode.ToUpperInvariant();
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AegisDbContext>();
        return await db.DiscoveredHosts
            .AsNoTracking()
            .Where(x =>
                x.CountryCode == code &&
                x.Lat != null &&
                x.Lng != null &&
                x.IsUp != false)
            .OrderByDescending(x => x.UpdatedAt)
            .Take(Math.Clamp(limit, 1, 2000))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<int> CountByCountryAsync(
        string countryCode,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(countryCode))
        {
            return 0;
        }

        var code = countryCode.ToUpperInvariant();
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AegisDbContext>();
        return await db.DiscoveredHosts
            .AsNoTracking()
            .CountAsync(x => x.CountryCode == code, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<DiscoveredHostEntity>> ListNeedingProbeAsync(
        double south,
        double north,
        double west,
        double east,
        DateTimeOffset probeBefore,
        int limit,
        CancellationToken cancellationToken = default)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AegisDbContext>();
        return (await db.DiscoveredHosts
            .Where(x =>
                x.Lat != null &&
                x.Lng != null &&
                x.Lat >= south &&
                x.Lat <= north &&
                x.Lng >= west &&
                x.Lng <= east &&
                (x.LastProbeAt == null || x.LastProbeAt < probeBefore))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false))
            .OrderBy(x => x.LastProbeAt ?? DateTimeOffset.MinValue)
            .Take(limit)
            .ToList();
    }

    public async Task<IReadOnlyList<string>> ListIpsWithoutCensysInBboxAsync(
        double south,
        double north,
        double west,
        double east,
        int limit,
        CancellationToken cancellationToken = default)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AegisDbContext>();
        return await db.DiscoveredHosts
            .AsNoTracking()
            .Where(x =>
                x.CensysFetchedAt == null &&
                x.Lat != null &&
                x.Lng != null &&
                x.Lat >= south &&
                x.Lat <= north &&
                x.Lng >= west &&
                x.Lng <= east)
            .OrderBy(x => x.CreatedAt)
            .Select(x => x.Ip)
            .Take(limit)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<string>> ListIpsWithoutCensysAsync(
        string countryCode,
        int limit,
        CancellationToken cancellationToken = default)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AegisDbContext>();
        return await db.DiscoveredHosts
            .AsNoTracking()
            .Where(x => x.CountryCode == countryCode && x.CensysFetchedAt == null)
            .OrderBy(x => x.CreatedAt)
            .Select(x => x.Ip)
            .Take(limit)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<CountryIngestStateEntity> GetOrCreateIngestStateAsync(
        string countryCode,
        CancellationToken cancellationToken = default)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AegisDbContext>();
        var state = await db.CountryIngestStates
            .FirstOrDefaultAsync(x => x.CountryCode == countryCode, cancellationToken)
            .ConfigureAwait(false);

        if (state is not null)
        {
            return state;
        }

        state = new CountryIngestStateEntity
        {
            CountryCode = countryCode,
            CidrCursor = 0,
            SearchComplete = false,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        db.CountryIngestStates.Add(state);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return state;
    }

    public async Task SaveIngestStateAsync(CountryIngestStateEntity state, CancellationToken cancellationToken = default)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AegisDbContext>();
        var existing = await db.CountryIngestStates
            .FirstOrDefaultAsync(x => x.CountryCode == state.CountryCode, cancellationToken)
            .ConfigureAwait(false);

        if (existing is null)
        {
            state.UpdatedAt = DateTimeOffset.UtcNow;
            db.CountryIngestStates.Add(state);
        }
        else
        {
            existing.CidrCursor = state.CidrCursor;
            existing.SearchPageToken = state.SearchPageToken;
            existing.SearchComplete = state.SearchComplete;
            existing.OverpassTileIndex = state.OverpassTileIndex;
            existing.OverpassWarmComplete = state.OverpassWarmComplete;
            existing.ShodanRegionIndex = state.ShodanRegionIndex;
            existing.ShodanWarmComplete = state.ShodanWarmComplete;
            existing.PrefetchWarmComplete = state.PrefetchWarmComplete;
            existing.LastPrefetchUtc = state.LastPrefetchUtc;
            existing.UpdatedAt = DateTimeOffset.UtcNow;
        }

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task MarkCensysAttemptedAsync(string ip, CancellationToken cancellationToken = default)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AegisDbContext>();
        var host = await db.DiscoveredHosts
            .FirstOrDefaultAsync(x => x.Ip == ip, cancellationToken)
            .ConfigureAwait(false);

        if (host is null)
        {
            return;
        }

        host.CensysFetchedAt = DateTimeOffset.UtcNow;
        host.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task SaveProbeResultAsync(
        string ip,
        bool isUp,
        CancellationToken cancellationToken = default)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AegisDbContext>();
        var host = await db.DiscoveredHosts.FirstOrDefaultAsync(x => x.Ip == ip, cancellationToken).ConfigureAwait(false);
        if (host is null)
        {
            return;
        }

        host.IsUp = isUp;
        host.LastProbeAt = DateTimeOffset.UtcNow;
        host.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
