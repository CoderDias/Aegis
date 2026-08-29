using System.Text.RegularExpressions;
using Aegis.Application.Dtos.Intel;
using Aegis.Infrastructure.Geo;
using Microsoft.Extensions.Logging;

namespace Aegis.Infrastructure.External.Weather;

public sealed class RussiaWeatherAlertClient(
    IHttpClientFactory httpClientFactory,
    ILogger<RussiaWeatherAlertClient> logger)
{
    private static readonly Regex HtmlTagRegex = new("<[^>]+>", RegexOptions.Compiled);
    private static readonly Regex WhitespaceRegex = new(@"\s+", RegexOptions.Compiled);

    public async Task<IReadOnlyList<GeoMarkerDto>> FetchActiveAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var client = httpClientFactory.CreateClient("RoshydrometAlerts");
            using var response = await client.GetAsync("hazardsbull", cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogDebug("Roshydromet avisos retornou {Status}", response.StatusCode);
                return [];
            }

            var html = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            var text = NormalizeText(html);
            if (string.IsNullOrWhiteSpace(text))
            {
                return [];
            }

            var markers = new List<GeoMarkerDto>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var (regionName, centroid) in RussiaRegionCentroids.MatchRegions(text))
            {
                if (!seen.Add(regionName))
                {
                    continue;
                }

                var snippet = ExtractRegionSnippet(text, regionName);
                markers.Add(new GeoMarkerDto(
                    $"ru-{regionName.GetHashCode(StringComparison.OrdinalIgnoreCase):X8}",
                    "weather_alert",
                    $"Aviso · {regionName}",
                    "Fenômeno adverso",
                    centroid.Lat,
                    centroid.Lng,
                    Weight: 2,
                    Timestamp: DateTimeOffset.UtcNow,
                    Detail: snippet,
                    Source: "Roshydromet · Rússia",
                    Severity: "Moderado",
                    Region: regionName,
                    EventType: "Fenômeno hidrometeorológico",
                    Risks: snippet));
            }

            return markers;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            logger.LogDebug(ex, "Roshydromet avisos indisponível.");
            return [];
        }
    }

    private static string NormalizeText(string html)
    {
        var withoutTags = HtmlTagRegex.Replace(html, " ");
        return WhitespaceRegex.Replace(withoutTags, " ").Trim();
    }

    private static string? ExtractRegionSnippet(string text, string regionName)
    {
        var index = text.IndexOf(regionName, StringComparison.OrdinalIgnoreCase);
        if (index < 0)
        {
            return null;
        }

        var start = Math.Max(0, index);
        var length = Math.Min(260, text.Length - start);
        var snippet = text.Substring(start, length).Trim();
        return snippet.Length <= 240 ? snippet : snippet[..240] + "…";
    }
}
