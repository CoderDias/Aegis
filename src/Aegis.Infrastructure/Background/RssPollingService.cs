using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Aegis.Application.Abstractions;
using Aegis.Application.Dtos.Intel;
using Aegis.Infrastructure.Data;
using Aegis.Infrastructure.Intel;
using Aegis.Infrastructure.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Aegis.Application.Settings;
using Aegis.Infrastructure.Settings;

namespace Aegis.Infrastructure.Background;

public sealed partial class RssPollingService(
    IServiceScopeFactory scopeFactory,
    IHttpClientFactory httpClientFactory,
    IntegrationSettingsService integrationSettings,
    IOptions<RssOptions> options,
    ILogger<RssPollingService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken).ConfigureAwait(false);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await SeedDefaultFeedsAsync(stoppingToken).ConfigureAwait(false);
                await PollAllFeedsAsync(stoppingToken).ConfigureAwait(false);
        await GeocodeRecentNewsAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "RSS polling cycle failed.");
            }

            await Task.Delay(TimeSpan.FromMinutes(options.Value.PollingIntervalMinutes), stoppingToken)
                .ConfigureAwait(false);
        }
    }

    private async Task SeedDefaultFeedsAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AegisDbContext>();
        var store = scope.ServiceProvider.GetRequiredService<IRssFeedStore>();
        var seeded = 0;
        var configuredUrls = options.Value.DefaultFeeds
            .Where(f => !string.IsNullOrWhiteSpace(f.Url))
            .Select(f => f.Url.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var feed in options.Value.DefaultFeeds)
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
        }

        const string defesaNetFeedUrl = "https://www.defesanet.com.br/categoria/defense/feed/";
        var canonicalDefesaNet = await db.RssFeeds
            .FirstOrDefaultAsync(f => f.Url == defesaNetFeedUrl, cancellationToken)
            .ConfigureAwait(false);

        foreach (var legacy in await db.RssFeeds
                     .Where(f => f.Title.Contains("DefesaNet") || f.Url.Contains("defesanet.com.br"))
                     .ToListAsync(cancellationToken)
                     .ConfigureAwait(false))
        {
            if (string.Equals(legacy.Url, defesaNetFeedUrl, StringComparison.OrdinalIgnoreCase))
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

            legacy.Url = defesaNetFeedUrl;
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
    }

    private async Task GeocodeRecentNewsAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var geocoder = scope.ServiceProvider.GetRequiredService<NewsGeocodingService>();
        await geocoder.GeocodePendingAsync(options.Value.GeocodeBatchSize, cancellationToken).ConfigureAwait(false);
    }

    private async Task PollAllFeedsAsync(CancellationToken cancellationToken)
    {
        if (!integrationSettings.IsEnabled(IntegrationKeys.Rss))
        {
            return;
        }

        await using var scope = scopeFactory.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IRssFeedStore>();
        var feeds = await store.ListAsync(cancellationToken).ConfigureAwait(false);

        foreach (var feed in feeds.Where(f => f.Enabled))
        {
            try
            {
                var items = await FetchFeedAsync(feed, cancellationToken).ConfigureAwait(false);
                if (items.Count > 0)
                {
                    await store.UpsertNewsItemsAsync(feed.Id, items, cancellationToken).ConfigureAwait(false);
                }

                await store.UpdateLastFetchedAsync(feed.Id, DateTimeOffset.UtcNow, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                await store.SetFeedEnabledAsync(feed.Id, false, cancellationToken).ConfigureAwait(false);
                logger.LogInformation("RSS feed {Title} disabled (404): {Url}", feed.Title, feed.Url);
            }
            catch (Exception ex) when (ex is InvalidOperationException or System.Xml.XmlException)
            {
                logger.LogDebug("RSS feed {Title} skipped: {Message}", feed.Title, ex.Message);
            }
            catch (Exception ex)
            {
                logger.LogDebug("RSS feed {Title} unavailable: {Message}", feed.Title, ex.Message);
            }
        }
    }

    private async Task<IReadOnlyList<NewsItemDto>> FetchFeedAsync(RssFeedDto feed, CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.UserAgent.ParseAdd(RssXmlHelper.FeedUserAgent);
        client.DefaultRequestHeaders.Accept.ParseAdd("application/rss+xml, application/atom+xml, application/xml, text/xml, */*");
        client.Timeout = TimeSpan.FromSeconds(15);

        using var response = await client.GetAsync(feed.Url, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var xml = await RssXmlHelper.ReadContentAsStringAsync(response.Content, cancellationToken)
            .ConfigureAwait(false);

        if (RssXmlHelper.IsHtmlResponse(xml))
        {
            throw new InvalidOperationException("Feed URL returned HTML instead of RSS/Atom XML.");
        }

        var doc = RssXmlHelper.ParseFeedDocument(xml);
        XNamespace atom = "http://www.w3.org/2005/Atom";

        var items = new List<NewsItemDto>();

        foreach (var item in doc.Descendants("item"))
        {
            items.Add(ParseRssItem(feed, item));
        }

        foreach (var entry in doc.Descendants(atom + "entry"))
        {
            items.Add(ParseAtomEntry(feed, entry, atom));
        }

        return items.Take(options.Value.MaxItemsPerFeed).ToList();
    }

    private static NewsItemDto ParseRssItem(RssFeedDto feed, XElement item)
    {
        var title = item.Element("title")?.Value?.Trim() ?? "Sem título";
        var link = item.Element("link")?.Value?.Trim() ?? string.Empty;
        var summary = item.Element("description")?.Value?.Trim();
        var pubDate = ParseDate(item.Element("pubDate")?.Value);
        var imageUrl = ExtractImageUrl(item, summary);

        return new NewsItemDto(
            Guid.NewGuid(),
            feed.Id,
            feed.Title,
            title,
            link,
            HtmlTextHelper.TruncatePlain(HtmlTextHelper.StripHtml(summary), 500),
            pubDate,
            null,
            null,
            imageUrl);
    }

    private static NewsItemDto ParseAtomEntry(RssFeedDto feed, XElement entry, XNamespace atom)
    {
        var title = entry.Element(atom + "title")?.Value?.Trim() ?? "Sem título";
        var link = entry.Element(atom + "link")?.Attribute("href")?.Value?.Trim() ?? string.Empty;
        var summary = entry.Element(atom + "summary")?.Value?.Trim()
            ?? entry.Element(atom + "content")?.Value?.Trim();
        var updated = entry.Element(atom + "updated")?.Value
            ?? entry.Element(atom + "published")?.Value;
        var imageUrl = ExtractImageUrl(entry, summary);

        return new NewsItemDto(
            Guid.NewGuid(),
            feed.Id,
            feed.Title,
            title,
            link,
            HtmlTextHelper.TruncatePlain(HtmlTextHelper.StripHtml(summary), 500),
            ParseDate(updated),
            null,
            null,
            imageUrl);
    }

    private static string? ExtractImageUrl(XElement element, string? htmlContent)
    {
        XNamespace media = "http://search.yahoo.com/mrss/";

        var mediaUrl = element.Element(media + "content")?.Attribute("url")?.Value
            ?? element.Element(media + "thumbnail")?.Attribute("url")?.Value;
        if (!string.IsNullOrWhiteSpace(mediaUrl))
        {
            return mediaUrl.Trim();
        }

        var enclosure = element.Element("enclosure");
        if (enclosure?.Attribute("type")?.Value?.StartsWith("image", StringComparison.OrdinalIgnoreCase) == true)
        {
            return enclosure.Attribute("url")?.Value?.Trim();
        }

        if (string.IsNullOrWhiteSpace(htmlContent))
        {
            return null;
        }

        var match = ImageTagRegex().Match(htmlContent);
        return match.Success ? match.Groups[1].Value.Trim() : null;
    }

    [GeneratedRegex("""<img[^>]+src=["']([^"']+)["']""", RegexOptions.IgnoreCase)]
    private static partial Regex ImageTagRegex();

    private static DateTimeOffset ParseDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return DateTimeOffset.UtcNow;
        }

        if (DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var dto))
        {
            return dto;
        }

        return DateTimeOffset.UtcNow;
    }

    private static string? Truncate(string? text, int max) =>
        string.IsNullOrEmpty(text) ? text : text.Length <= max ? text : text[..max];
}

public sealed class RssOptions
{
    public const string SectionName = "Rss";

    public int PollingIntervalMinutes { get; set; } = 30;

    public int MaxItemsPerFeed { get; set; } = 30;

    public int GeocodeBatchSize { get; set; } = 40;

    public int MaxNewsMarkers { get; set; } = 2000;

    public int NewsCacheMinutes { get; set; } = 15;

    public List<RssFeedSeed> DefaultFeeds { get; set; } = [];
}

public sealed class RssFeedSeed
{
    public string Title { get; set; } = string.Empty;

    public string Url { get; set; } = string.Empty;

    public string? RegionQuery { get; set; }
}
