using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Aegis.Infrastructure.Geo;

public sealed class RepeaterBookClient(
    IHttpClientFactory httpClientFactory,
    IOptions<RepeaterBookOptions> options,
    ILogger<RepeaterBookClient> logger)
{
    private static readonly Regex IdRegex = new(@"ID=(\d+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex CoordRegex = new(
        @"prox2_result\.php\?lat=([^&""']+)&long=([^&""']+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex RowRegex = new(
        @"details\.php\?state_id=(?<state>[A-Z]{2})&amp;ID=(?<id>\d+)[^""]*""[^>]*>\s*(?<freq>[\d.]+)\s*</a>[\s\S]*?<td[^>]*>(?<access>[^<]*)</td>[\s\S]*?<td[^>]*>(?<location>[^<]*)</td>[\s\S]*?<td[^>]*>(?<callsign>[^<]*)</td>[\s\S]*?<td[^>]*>(?<mode>[^<]*)</td>",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public async Task<IReadOnlyList<RepeaterBookCatalogEntry>> FetchBrazilRepeatersAsync(
        CancellationToken cancellationToken = default)
    {
        if (!options.Value.Enabled)
        {
            return [];
        }

        var stateId = options.Value.StateId.Trim().ToUpperInvariant();
        var listUrl =
            $"https://www.repeaterbook.com/row_repeaters/Display_SS.php?state_id={stateId}&mode=2&lang=es&include_simplex=1";

        try
        {
            var client = CreateClient();
            using var listResponse = await client.GetAsync(listUrl, cancellationToken).ConfigureAwait(false);
            if (!listResponse.IsSuccessStatusCode)
            {
                logger.LogWarning("RepeaterBook list returned {Status}", listResponse.StatusCode);
                return [];
            }

            var listHtml = await listResponse.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            var metadata = ParseListMetadata(listHtml, stateId);
            var ids = metadata.Keys.ToList();
            if (ids.Count == 0)
            {
                ids = IdRegex.Matches(listHtml)
                    .Select(m => int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture))
                    .Distinct()
                    .ToList();
            }

            var results = new List<RepeaterBookCatalogEntry>();
            foreach (var id in ids)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await Task.Delay(250, cancellationToken).ConfigureAwait(false);

                var detailUrl = $"https://www.repeaterbook.com/row_repeaters/details.php?state_id={stateId}&ID={id}";
                using var detailResponse = await client.GetAsync(detailUrl, cancellationToken).ConfigureAwait(false);
                if (!detailResponse.IsSuccessStatusCode)
                {
                    continue;
                }

                var detailHtml = await detailResponse.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                if (!TryParseCoordinates(detailHtml, out var lat, out var lng))
                {
                    continue;
                }

                metadata.TryGetValue(id, out var meta);
                var onAir = detailHtml.Contains("On-Air", StringComparison.OrdinalIgnoreCase) &&
                            !detailHtml.Contains("Off-air", StringComparison.OrdinalIgnoreCase);

                results.Add(new RepeaterBookCatalogEntry
                {
                    Id = id,
                    Callsign = meta?.Callsign,
                    Frequency = meta?.Frequency ?? ExtractFrequency(detailHtml),
                    Location = meta?.Location,
                    Mode = meta?.Mode,
                    Lat = lat,
                    Lng = lng,
                    OnAir = onAir
                });
            }

            return results;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "RepeaterBook fetch failed.");
            return [];
        }
    }

    internal static Dictionary<int, RepeaterBookListMeta> ParseListMetadata(string html, string stateId)
    {
        var map = new Dictionary<int, RepeaterBookListMeta>();
        foreach (Match match in RowRegex.Matches(html))
        {
            if (!string.Equals(match.Groups["state"].Value, stateId, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var id = int.Parse(match.Groups["id"].Value, CultureInfo.InvariantCulture);
            map[id] = new RepeaterBookListMeta
            {
                Frequency = match.Groups["freq"].Value.Trim(),
                Location = WebUtility.HtmlDecode(match.Groups["location"].Value.Trim()),
                Callsign = WebUtility.HtmlDecode(match.Groups["callsign"].Value.Trim()),
                Mode = WebUtility.HtmlDecode(match.Groups["mode"].Value.Trim())
            };
        }

        return map;
    }

    internal static bool TryParseCoordinates(string html, out double lat, out double lng)
    {
        lat = 0;
        lng = 0;
        var match = CoordRegex.Match(html);
        if (!match.Success)
        {
            return false;
        }

        return double.TryParse(match.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out lat) &&
               double.TryParse(match.Groups[2].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out lng) &&
               lat is >= -35 and <= 6 &&
               lng is >= -75 and <= -28;
    }

    private static string? ExtractFrequency(string html)
    {
        var match = Regex.Match(html, @"(\d{3}\.\d+)\s*MHz", RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[1].Value : null;
    }

    private HttpClient CreateClient()
    {
        var client = httpClientFactory.CreateClient("RepeaterBook");
        client.Timeout = TimeSpan.FromSeconds(30);
        return client;
    }

    internal sealed class RepeaterBookListMeta
    {
        public string? Frequency { get; init; }
        public string? Location { get; init; }
        public string? Callsign { get; init; }
        public string? Mode { get; init; }
    }
}

public sealed class RepeaterBookCatalogEntry
{
    public long Id { get; set; }
    public string? Callsign { get; set; }
    public string? Frequency { get; set; }
    public string? Location { get; set; }
    public string? Mode { get; set; }
    public double Lat { get; set; }
    public double Lng { get; set; }
    public bool OnAir { get; set; } = true;
}
