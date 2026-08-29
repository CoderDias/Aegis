using Aegis.Application.Abstractions;
using Aegis.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Aegis.Infrastructure.Intel;

public sealed class NewsGeocodingService(
    AegisDbContext db,
    IGeocodingService geocoding,
    IRssFeedStore newsStore,
    ILogger<NewsGeocodingService> logger)
{
    public async Task GeocodePendingAsync(int maxItems, CancellationToken cancellationToken = default)
    {
        var pending = (await db.NewsItems
            .Include(n => n.Feed)
            .Where(n => n.Latitude == null || n.Longitude == null)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false))
            .OrderByDescending(n => n.FetchedAt)
            .Take(maxItems)
            .ToList();

        var changed = false;
        foreach (var item in pending)
        {
            var query = NewsPlaceExtractor.ExtractGeocodeQuery(item.Title, item.Summary)
                ?? item.Feed.DefaultRegionQuery;
            if (string.IsNullOrWhiteSpace(query))
            {
                continue;
            }

            try
            {
                var results = await geocoding.SearchAsync(query, 1, cancellationToken).ConfigureAwait(false);
                var hit = results.FirstOrDefault();
                if (hit is null)
                {
                    continue;
                }

                item.Latitude = hit.Coordinate.Lat;
                item.Longitude = hit.Coordinate.Lng;
                changed = true;
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "Geocode failed for news item {Title}", item.Title);
            }
        }

        if (changed)
        {
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            newsStore.InvalidateNewsCache();
        }
    }
}
