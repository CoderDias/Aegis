using Aegis.Application.Abstractions;
using Microsoft.Extensions.Caching.Memory;

namespace Aegis.Infrastructure.Geo;

public sealed class AirportCoordinateProvider(
    IGeocodingService geocoding,
    IMemoryCache cache)
{
    private static readonly Dictionary<string, (double Lat, double Lng, string Name)> Known =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["SBGR"] = (-23.435556, -46.473056, "São Paulo/Guarulhos"),
            ["SBGL"] = (-22.809999, -43.250000, "Rio/Galeão"),
            ["SBBR"] = (-15.871111, -47.918611, "Brasília"),
            ["SBSP"] = (-23.626111, -46.656389, "Congonhas"),
            ["SBKP"] = (-23.007778, -47.134444, "Campinas/Viracopos"),
            ["SBCF"] = (-19.624444, -43.971944, "Belo Horizonte/Confins"),
            ["SBPA"] = (-29.994444, -51.171389, "Porto Alegre"),
            ["SBSV"] = (-12.908611, -38.322500, "Salvador"),
            ["SBRF"] = (-8.126389, -34.923611, "Recife"),
            ["SBCT"] = (-25.528475, -49.175775, "Curitiba"),
            ["SBFL"] = (-27.670489, -48.547181, "Florianópolis"),
            ["SBEG"] = (-3.038611, -60.049722, "Manaus"),
            ["SBFZ"] = (-3.776283, -38.532556, "Fortaleza"),
            ["SBVT"] = (-20.258056, -40.286389, "Vitória"),
            ["EGLL"] = (51.470020, -0.454295, "London Heathrow"),
            ["EGKK"] = (51.153662, -0.182063, "London Gatwick"),
            ["LFPG"] = (49.009690, 2.547925, "Paris CDG"),
            ["EDDF"] = (50.037933, 8.562152, "Frankfurt"),
            ["EHAM"] = (52.308601, 4.763889, "Amsterdam Schiphol"),
            ["LEMD"] = (40.493556, -3.566764, "Madrid Barajas"),
            ["LIRF"] = (41.800278, 12.238889, "Rome Fiumicino"),
            ["KJFK"] = (40.639751, -73.778925, "New York JFK"),
            ["KLAX"] = (33.942536, -118.408075, "Los Angeles"),
            ["KORD"] = (41.978603, -87.904842, "Chicago O'Hare"),
            ["KATL"] = (33.636719, -84.428067, "Atlanta"),
            ["KMIA"] = (25.793449, -80.290556, "Miami"),
            ["KDFW"] = (32.896828, -97.037997, "Dallas/Fort Worth"),
            ["KSFO"] = (37.618972, -122.374889, "San Francisco"),
            ["CYYZ"] = (43.677223, -79.630556, "Toronto Pearson"),
            ["OMDB"] = (25.252778, 55.364444, "Dubai"),
            ["OTHH"] = (25.273056, 51.608056, "Doha Hamad"),
            ["RJTT"] = (35.552258, 139.779694, "Tokyo Haneda"),
            ["RJAA"] = (35.764722, 140.386389, "Tokyo Narita"),
            ["ZSPD"] = (31.143378, 121.805214, "Shanghai Pudong"),
            ["ZBAA"] = (40.079857, 116.603112, "Beijing Capital"),
            ["VHHH"] = (22.308919, 113.914603, "Hong Kong"),
            ["WSSS"] = (1.364420, 103.991531, "Singapore Changi"),
            ["YSSY"] = (-33.946111, 151.177222, "Sydney"),
            ["LTFM"] = (41.275278, 28.751944, "Istanbul"),
            ["LOWW"] = (48.110278, 16.569722, "Vienna"),
            ["LSZH"] = (47.464722, 8.549167, "Zurich"),
            ["UUEE"] = (55.972642, 37.414589, "Moscow Sheremetyevo"),
            ["SAEZ"] = (-34.822222, -58.535833, "Buenos Aires Ezeiza"),
            ["SCEL"] = (-33.392975, -70.785803, "Santiago"),
            ["SPJC"] = (-12.021889, -77.114319, "Lima Jorge Chávez"),
            ["SKBO"] = (4.701594, -74.146947, "Bogotá El Dorado"),
            ["MMMX"] = (19.436303, -99.072097, "Mexico City"),
            ["FACT"] = (-33.964806, 18.601667, "Cape Town"),
            ["FAOR"] = (-26.139166, 28.246000, "Johannesburg"),
        };

    public async Task<(double Lat, double Lng, string Label)?> ResolveAsync(
        string icao,
        CancellationToken cancellationToken = default)
    {
        var code = icao.Trim().ToUpperInvariant();
        if (code.Length is not (3 or 4))
        {
            return null;
        }

        if (Known.TryGetValue(code, out var known))
        {
            return (known.Lat, known.Lng, $"{code} — {known.Name}");
        }

        var cacheKey = $"airport:icao:{code}";
        if (cache.TryGetValue(cacheKey, out (double Lat, double Lng, string Label) cached))
        {
            return cached;
        }

        var results = await geocoding
            .SearchAsync($"{code} airport", limit: 3, cancellationToken)
            .ConfigureAwait(false);

        var match = results.FirstOrDefault();
        if (match is null)
        {
            return null;
        }

        var resolved = (match.Coordinate.Lat, match.Coordinate.Lng, $"{code} — {match.DisplayName}");
        cache.Set(cacheKey, resolved, TimeSpan.FromDays(7));
        return resolved;
    }
}
