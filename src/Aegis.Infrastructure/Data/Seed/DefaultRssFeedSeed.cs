using Aegis.Application.Abstractions;
using Aegis.Infrastructure.Background;
using Aegis.Infrastructure.Data;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Aegis.Infrastructure.Data.Seed;

public static class DefaultRssFeedSeed
{
    public static async Task SeedAsync(
        AegisDbContext db,
        IRssFeedStore store,
        IOptions<RssOptions> options,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        if (options.Value.DefaultFeeds.Count == 0)
        {
            logger.LogDebug("No default RSS feeds configured; skipping seed.");
            return;
        }

        await RssFeedSeeder.SyncAsync(db, store, options.Value.DefaultFeeds, logger, cancellationToken)
            .ConfigureAwait(false);
    }
}
