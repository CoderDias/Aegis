using System.Text.Json;
using System.Text.RegularExpressions;
using Aegis.Application.Dtos.Investigations;
using Aegis.Application.Dtos.Intel;
using Aegis.Application.Dtos.Osint;
using Aegis.Application.Osint;
using Aegis.Domain.Enums;

namespace Aegis.Application.Services;

public static partial class OsintCorrelationService
{
    public static IReadOnlyList<OsintCorrelationHit> FindHits(
        IReadOnlyList<AssetDto> assets,
        IReadOnlyList<ShodanHostDto> hosts,
        IReadOnlyList<NewsItemDto> news)
    {
        var hits = new List<OsintCorrelationHit>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var asset in assets)
        {
            var context = OsintContextExtractor.FromAssetMetadata(asset.Type, asset.DisplayName, asset.MetadataJson);
            AddHit(hits, seen, "asset", asset.DisplayName, "CNPJ", context.Cnpj, asset.Id.ToString("N"));
            AddHit(hits, seen, "asset", asset.DisplayName, "Domínio", context.Domain, asset.Id.ToString("N"));
            AddHit(hits, seen, "asset", asset.DisplayName, "IP", context.Ip, asset.Id.ToString("N"));

            if (asset.Type == AssetType.Host && IpRegex().IsMatch(asset.DisplayName))
            {
                foreach (var host in hosts.Where(h => string.Equals(h.Ip, asset.DisplayName, StringComparison.OrdinalIgnoreCase)))
                {
                    AddHit(hits, seen, "host", host.Ip, "IP", host.Ip, host.Ip);
                }
            }

            foreach (var item in news)
            {
                if (MatchesNews(item, context))
                {
                    AddHit(hits, seen, "news", item.Title, "Notícia", context.Cnpj ?? context.Domain ?? context.Ip, item.Id.ToString("N"));
                }
            }
        }

        return hits
            .OrderByDescending(h => h.Strength)
            .ThenBy(h => h.SourceLabel, StringComparer.OrdinalIgnoreCase)
            .Take(20)
            .ToList();
    }

    private static bool MatchesNews(NewsItemDto item, OsintContext context)
    {
        var haystack = $"{item.Title} {item.Summary}";
        if (context.Cnpj is not null && haystack.Contains(context.Cnpj, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (context.Domain is not null && haystack.Contains(context.Domain, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return context.Ip is not null && haystack.Contains(context.Ip, StringComparison.OrdinalIgnoreCase);
    }

    private static void AddHit(
        List<OsintCorrelationHit> hits,
        HashSet<string> seen,
        string sourceKind,
        string sourceLabel,
        string matchType,
        string? value,
        string sourceId)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        var key = $"{matchType}:{value}:{sourceKind}:{sourceId}";
        if (!seen.Add(key))
        {
            return;
        }

        hits.Add(new OsintCorrelationHit(sourceKind, sourceLabel, matchType, value, sourceId, Strength: matchType switch
        {
            "IP" => 3,
            "CNPJ" => 2,
            _ => 1
        }));
    }

    public static string Serialize(IReadOnlyList<OsintCorrelationHit> hits) =>
        JsonSerializer.Serialize(hits);

    [GeneratedRegex(@"^\d{1,3}(\.\d{1,3}){3}$")]
    private static partial Regex IpRegex();
}

public sealed record OsintCorrelationHit(
    string SourceKind,
    string SourceLabel,
    string MatchType,
    string Value,
    string SourceId,
    int Strength);
