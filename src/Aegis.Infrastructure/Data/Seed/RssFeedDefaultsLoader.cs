using System.Text.Json;
using Aegis.Infrastructure.Background;
using Microsoft.Extensions.Logging;

namespace Aegis.Infrastructure.Data.Seed;

public static class RssFeedDefaultsLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static IReadOnlyList<RssFeedSeed> Load(ILogger? logger = null)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Data", "rss-feeds.default.json");
        if (!File.Exists(path))
        {
            logger?.LogWarning("Default RSS feed catalog not found at {Path}", path);
            return [];
        }

        try
        {
            var json = File.ReadAllText(path);
            var feeds = JsonSerializer.Deserialize<List<RssFeedSeed>>(json, JsonOptions) ?? [];
            return feeds
                .Where(f => !string.IsNullOrWhiteSpace(f.Url))
                .ToList();
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "Failed to load default RSS feed catalog from {Path}", path);
            return [];
        }
    }

    public static List<RssFeedSeed> Merge(IReadOnlyList<RssFeedSeed> bundled, IReadOnlyList<RssFeedSeed> configured)
    {
        var merged = new Dictionary<string, RssFeedSeed>(StringComparer.OrdinalIgnoreCase);

        foreach (var feed in bundled)
        {
            if (string.IsNullOrWhiteSpace(feed.Url))
            {
                continue;
            }

            merged[feed.Url.Trim()] = feed;
        }

        foreach (var feed in configured)
        {
            if (string.IsNullOrWhiteSpace(feed.Url))
            {
                continue;
            }

            merged[feed.Url.Trim()] = feed;
        }

        return merged.Values.ToList();
    }
}
