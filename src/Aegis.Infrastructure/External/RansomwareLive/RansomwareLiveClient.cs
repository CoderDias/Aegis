using System.Text.Json;
using Aegis.Application.Dtos.Intel;
using Microsoft.Extensions.Logging;

namespace Aegis.Infrastructure.External.RansomwareLive;

public sealed class RansomwareLiveClient(
    IHttpClientFactory httpClientFactory,
    ILogger<RansomwareLiveClient> logger)
{
    private static readonly Dictionary<string, (double Lat, double Lng)> CountryCentroids = new(StringComparer.OrdinalIgnoreCase)
    {
        ["BR"] = (-14.235, -51.925),
        ["US"] = (37.090, -95.713),
        ["GB"] = (55.378, -3.436),
        ["DE"] = (51.166, 10.452),
        ["FR"] = (46.228, 2.214),
        ["IT"] = (41.872, 12.567),
        ["ES"] = (40.464, -3.749),
        ["CA"] = (56.130, -106.347),
        ["AU"] = (-25.274, 133.775),
        ["JP"] = (36.205, 138.253),
        ["CN"] = (35.862, 104.195),
        ["IN"] = (20.594, 78.963),
        ["AR"] = (-38.416, -63.617),
        ["MX"] = (23.634, -102.553),
        ["NL"] = (52.133, 5.291),
        ["BE"] = (50.504, 4.470),
        ["CH"] = (46.818, 8.228),
        ["AT"] = (47.516, 14.550),
        ["SE"] = (60.128, 18.644),
        ["NO"] = (60.472, 8.469),
        ["PT"] = (39.400, -8.224),
        ["PL"] = (51.920, 19.145),
        ["RU"] = (61.524, 105.319),
        ["KR"] = (35.908, 127.767),
        ["ZA"] = (-30.559, 22.937),
        ["CL"] = (-35.675, -71.543),
        ["CO"] = (4.571, -74.297),
        ["PE"] = (-9.190, -75.015),
        ["MY"] = (4.210, 101.976),
        ["ID"] = (-0.790, 113.921),
        ["TH"] = (15.870, 100.993),
        ["VN"] = (14.058, 108.277),
        ["PH"] = (12.879, 121.774),
        ["SG"] = (1.352, 103.820),
        ["AE"] = (23.425, 53.848),
        ["SA"] = (23.886, 45.079),
        ["IL"] = (31.046, 34.852),
        ["EG"] = (26.821, 30.802),
        ["NG"] = (9.082, 8.675),
        ["KE"] = (-0.024, 37.907),
        ["TW"] = (23.698, 120.961),
        ["RO"] = (45.943, 24.967),
        ["CZ"] = (49.818, 15.473),
        ["HU"] = (47.162, 19.503),
        ["GR"] = (39.074, 21.824),
        ["DK"] = (56.264, 9.502),
        ["FI"] = (61.924, 25.748),
        ["IE"] = (53.413, -8.244),
        ["NZ"] = (-40.901, 174.886),
        ["QA"] = (25.355, 51.184),
        ["RS"] = (44.017, 20.907),
        ["GT"] = (15.784, -90.231),
        ["GA"] = (-0.804, 11.609),
    };

    public async Task<IReadOnlyList<RansomwareVictimDto>> GetRecentVictimsAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            var client = httpClientFactory.CreateClient("RansomwareLive");
            using var response = await client.GetAsync("recentvictims", cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogDebug("RansomwareLive API returned {Status}", response.StatusCode);
                return [];
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            var victims = await JsonSerializer.DeserializeAsync<List<RansomwareLiveVictim>>(
                stream,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true },
                cancellationToken).ConfigureAwait(false);

            if (victims is null) return [];

            return victims
                .Where(v => !string.IsNullOrWhiteSpace(v.Victim))
                .Select(MapToDto)
                .Where(d => d.Lat is not null)
                .Take(100)
                .ToList();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to fetch ransomware.live victims");
            return [];
        }
    }

    private static RansomwareVictimDto MapToDto(RansomwareLiveVictim v)
    {
        double? lat = null, lng = null;
        if (!string.IsNullOrWhiteSpace(v.Country) && CountryCentroids.TryGetValue(v.Country, out var coords))
        {
            var jitter = new Random(v.Victim?.GetHashCode() ?? 0);
            lat = coords.Lat + (jitter.NextDouble() - 0.5) * 4;
            lng = coords.Lng + (jitter.NextDouble() - 0.5) * 4;
        }

        return new RansomwareVictimDto(
            v.Victim ?? "Unknown",
            v.Group ?? "Unknown",
            v.Country,
            v.Domain,
            v.Activity,
            v.Url ?? "",
            v.Discovered ?? DateTimeOffset.UtcNow,
            lat,
            lng);
    }

    private sealed class RansomwareLiveVictim
    {
        public string? Victim { get; set; }
        public string? Group { get; set; }
        public string? Country { get; set; }
        public string? Domain { get; set; }
        public string? Activity { get; set; }
        public string? Url { get; set; }
        public DateTimeOffset? Discovered { get; set; }
    }
}
