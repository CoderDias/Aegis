using System.Security.Cryptography;
using System.Text;
using Aegis.Application.Abstractions;
using Aegis.Application.Dtos.Geo;
using Aegis.Application.Dtos.Intel;
using Aegis.Infrastructure.Background;
using Aegis.Infrastructure.Data;
using Aegis.Infrastructure.Data.Entities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace Aegis.Infrastructure.Repositories;

public sealed class RssFeedStore(
    AegisDbContext db,
    IMemoryCache cache,
    IOptions<RssOptions> options) : IRssFeedStore
{
    private const string GeolocatedNewsCacheKey = "news:geolocated:all";

    public async Task<IReadOnlyList<RssFeedDto>> ListAsync(CancellationToken cancellationToken = default) =>
        await db.RssFeeds
            .AsNoTracking()
            .OrderBy(f => f.Title)
            .Select(f => new RssFeedDto(
                f.Id,
                f.Title,
                f.Url,
                f.Enabled,
                f.LastFetchedAt,
                f.DefaultRegionQuery,
                f.NewsItems.Count,
                f.NewsItems.Count(n => n.Latitude != null && n.Longitude != null)))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task<RssFeedDto> CreateAsync(CreateRssFeedRequest request, CancellationToken cancellationToken = default)
    {
        var title = request.Title.Trim();
        var url = request.Url.Trim();
        await EnsureUrlAvailableAsync(url, excludeId: null, cancellationToken).ConfigureAwait(false);

        var entity = new RssFeedEntity
        {
            Id = Guid.NewGuid(),
            Title = title,
            Url = url,
            DefaultRegionQuery = NormalizeRegionQuery(request.RegionQuery),
            Enabled = true,
            CreatedAt = DateTimeOffset.UtcNow
        };

        db.RssFeeds.Add(entity);
        try
        {
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
        {
            throw new InvalidOperationException("Já existe um feed com esta URL.", ex);
        }

        return await GetFeedDtoAsync(entity.Id, cancellationToken).ConfigureAwait(false);
    }

    public async Task<RssFeedDto> UpdateAsync(
        Guid id,
        UpdateRssFeedRequest request,
        CancellationToken cancellationToken = default)
    {
        var title = request.Title.Trim();
        var url = request.Url.Trim();
        var regionQuery = NormalizeRegionQuery(request.RegionQuery);

        await EnsureUrlAvailableAsync(url, excludeId: id, cancellationToken).ConfigureAwait(false);

        try
        {
            var rows = await db.RssFeeds
                .Where(f => f.Id == id)
                .ExecuteUpdateAsync(
                    s => s
                        .SetProperty(f => f.Title, title)
                        .SetProperty(f => f.Url, url)
                        .SetProperty(f => f.DefaultRegionQuery, regionQuery),
                    cancellationToken)
                .ConfigureAwait(false);

            if (rows == 0)
            {
                throw new InvalidOperationException($"RSS feed {id} not found.");
            }
        }
        catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
        {
            throw new InvalidOperationException("Já existe um feed com esta URL.", ex);
        }

        return await GetFeedDtoAsync(id, cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await db.RssFeeds.Where(f => f.Id == id).ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
        InvalidateNewsCache();
    }

    public async Task SetFeedEnabledAsync(Guid id, bool enabled, CancellationToken cancellationToken = default)
    {
        await db.RssFeeds
            .Where(f => f.Id == id)
            .ExecuteUpdateAsync(s => s.SetProperty(f => f.Enabled, enabled), cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task UpdateLastFetchedAsync(Guid id, DateTimeOffset fetchedAt, CancellationToken cancellationToken = default)
    {
        await db.RssFeeds
            .Where(f => f.Id == id)
            .ExecuteUpdateAsync(s => s.SetProperty(f => f.LastFetchedAt, fetchedAt), cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task UpsertNewsItemsAsync(
        Guid feedId,
        IReadOnlyList<NewsItemDto> items,
        CancellationToken cancellationToken = default)
    {
        if (items.Count == 0)
        {
            return;
        }

        var pendingHashes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in items)
        {
            if (string.IsNullOrWhiteSpace(item.Link))
            {
                continue;
            }

            var hash = HashLink(item.Link);
            if (!pendingHashes.Add(hash))
            {
                continue;
            }

            var exists = await db.NewsItems
                .AnyAsync(n => n.LinkHash == hash, cancellationToken)
                .ConfigureAwait(false);
            if (exists)
            {
                continue;
            }

            db.NewsItems.Add(new NewsItemEntity
            {
                Id = Guid.NewGuid(),
                FeedId = feedId,
                Title = item.Title,
                Link = item.Link,
                Summary = item.Summary,
                ImageUrl = item.ImageUrl,
                PublishedAt = item.PublishedAt,
                Latitude = item.Lat,
                Longitude = item.Lng,
                FetchedAt = DateTimeOffset.UtcNow,
                LinkHash = hash
            });
        }

        if (!db.ChangeTracker.HasChanges())
        {
            return;
        }

        try
        {
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            InvalidateNewsCache();
        }
        catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
        {
            db.ChangeTracker.Clear();
        }
    }

    public async Task<IReadOnlyList<NewsItemDto>> ListNewsAsync(int limit, CancellationToken cancellationToken = default)
    {
        var items = await db.NewsItems
            .AsNoTracking()
            .Include(n => n.Feed)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return items
            .OrderByDescending(n => n.PublishedAt)
            .Take(limit)
            .Select(MapNewsItem)
            .ToList();
    }

    public async Task<IReadOnlyList<NewsItemDto>> ListGeolocatedNewsAsync(CancellationToken cancellationToken = default)
    {
        if (cache.TryGetValue(GeolocatedNewsCacheKey, out IReadOnlyList<NewsItemDto>? cached) && cached is not null)
        {
            return cached;
        }

        var limit = Math.Clamp(options.Value.MaxNewsMarkers, 100, 5000);
        var rows = await db.NewsItems
            .AsNoTracking()
            .Include(n => n.Feed)
            .Where(n => n.Latitude != null && n.Longitude != null)
            .Select(n => new NewsItemDto(
                n.Id,
                n.FeedId,
                n.Feed.Title,
                n.Title,
                n.Link,
                n.Summary,
                n.PublishedAt,
                n.Latitude,
                n.Longitude,
                n.ImageUrl))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var items = rows
            .OrderByDescending(n => n.PublishedAt)
            .Take(limit)
            .ToList();

        cache.Set(
            GeolocatedNewsCacheKey,
            items,
            TimeSpan.FromMinutes(Math.Clamp(options.Value.NewsCacheMinutes, 1, 120)));

        return items;
    }

    public async Task<IReadOnlyList<NewsItemDto>> ListNewsInViewportAsync(
        BoundingBoxDto bbox,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var all = await ListGeolocatedNewsAsync(cancellationToken).ConfigureAwait(false);
        return all
            .Where(n => n.Lat >= bbox.South && n.Lat <= bbox.North &&
                        n.Lng >= bbox.West && n.Lng <= bbox.East)
            .Take(limit)
            .ToList();
    }

    public void InvalidateNewsCache() => cache.Remove(GeolocatedNewsCacheKey);

    private static NewsItemDto MapNewsItem(NewsItemEntity n) =>
        new(
            n.Id,
            n.FeedId,
            n.Feed.Title,
            n.Title,
            n.Link,
            n.Summary,
            n.PublishedAt,
            n.Latitude,
            n.Longitude,
            n.ImageUrl);

    private static bool IsUniqueConstraintViolation(DbUpdateException ex) =>
        ex.InnerException is SqliteException { SqliteErrorCode: 19 };

    private async Task EnsureUrlAvailableAsync(
        string url,
        Guid? excludeId,
        CancellationToken cancellationToken)
    {
        var normalized = url.Trim();
        var query = db.RssFeeds.AsNoTracking().Where(f => f.Url == normalized);
        if (excludeId.HasValue)
        {
            query = query.Where(f => f.Id != excludeId.Value);
        }

        if (await query.AnyAsync(cancellationToken).ConfigureAwait(false))
        {
            throw new InvalidOperationException("Já existe um feed com esta URL.");
        }
    }

    private async Task<RssFeedDto> GetFeedDtoAsync(Guid id, CancellationToken cancellationToken) =>
        await db.RssFeeds
            .AsNoTracking()
            .Where(f => f.Id == id)
            .Select(f => new RssFeedDto(
                f.Id,
                f.Title,
                f.Url,
                f.Enabled,
                f.LastFetchedAt,
                f.DefaultRegionQuery,
                f.NewsItems.Count,
                f.NewsItems.Count(n => n.Latitude != null && n.Longitude != null)))
            .FirstAsync(cancellationToken)
            .ConfigureAwait(false);

    private static string? NormalizeRegionQuery(string? query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return null;
        }

        return query.Trim();
    }

    private static string HashLink(string link)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(link.Trim().ToLowerInvariant()));
        return Convert.ToHexString(bytes);
    }
}
