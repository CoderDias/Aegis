using Aegis.Application.Abstractions;
using Aegis.Application.Dtos.Intel;
using Aegis.Infrastructure.Background;
using Aegis.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Aegis.Infrastructure.Data.Seed;

public static class RssFeedSeeder
{
    private const string DefesaNetFeedUrl = "https://www.defesanet.com.br/categoria/defense/feed/";

    public static async Task<int> SyncAsync(
        AegisDbContext db,
        IRssFeedStore store,
        IReadOnlyList<RssFeedSeed> defaultFeeds,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        var seeded = 0;
        var configuredUrls = defaultFeeds
            .Where(f => !string.IsNullOrWhiteSpace(f.Url))
            .Select(f => f.Url.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var feed in defaultFeeds)
        {
            if (string.IsNullOrWhiteSpace(feed.Url))
            {
                continue;
            }

            var url = feed.Url.Trim();
            var existing = await db.RssFeeds
                .FirstOrDefaultAsync(f => f.Url == url, cancellationToken)
                .ConfigureAwait(false);

            if (existing is null)
            {
                await store.CreateAsync(new CreateRssFeedRequest(feed.Title, url, feed.RegionQuery), cancellationToken)
                    .ConfigureAwait(false);
                seeded++;
                continue;
            }

            var regionQuery = string.IsNullOrWhiteSpace(feed.RegionQuery) ? null : feed.RegionQuery.Trim();
            if (existing.Title != feed.Title.Trim())
            {
                existing.Title = feed.Title.Trim();
            }

            if (existing.DefaultRegionQuery != regionQuery)
            {
                existing.DefaultRegionQuery = regionQuery;
            }

            if (!existing.Enabled)
            {
                existing.Enabled = true;
            }
        }

        var canonicalDefesaNet = await db.RssFeeds
            .FirstOrDefaultAsync(f => f.Url == DefesaNetFeedUrl, cancellationToken)
            .ConfigureAwait(false);

        foreach (var legacy in await db.RssFeeds
                     .Where(f => f.Title.Contains("DefesaNet") || f.Url.Contains("defesanet.com.br"))
                     .ToListAsync(cancellationToken)
                     .ConfigureAwait(false))
        {
            if (string.Equals(legacy.Url, DefesaNetFeedUrl, StringComparison.OrdinalIgnoreCase))
            {
                legacy.Title = "DefesaNet";
                legacy.Enabled = true;
                continue;
            }

            if (canonicalDefesaNet is not null && canonicalDefesaNet.Id != legacy.Id)
            {
                legacy.Enabled = false;
                continue;
            }

            legacy.Url = DefesaNetFeedUrl;
            legacy.Title = "DefesaNet";
            legacy.Enabled = true;
            canonicalDefesaNet = legacy;
        }

        var disabled = 0;
        foreach (var orphan in await db.RssFeeds
                     .Where(f => f.Enabled)
                     .ToListAsync(cancellationToken)
                     .ConfigureAwait(false))
        {
            if (!configuredUrls.Contains(orphan.Url))
            {
                orphan.Enabled = false;
                disabled++;
            }
        }

        if (seeded > 0)
        {
            logger.LogInformation("Seeded {Count} default RSS feeds", seeded);
        }

        if (disabled > 0)
        {
            logger.LogInformation("Disabled {Count} RSS feeds no longer in configuration", disabled);
        }

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return seeded;
    }
}
