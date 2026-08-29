using Aegis.Application.Dtos.Geo;

namespace Aegis.Infrastructure.Geo;

/// <summary>Bounding boxes aproximados (WGS84) para prefetch regional em background.</summary>
public static class CountryBoundingBoxCatalog
{
    private static readonly IReadOnlyDictionary<string, BoundingBoxDto> Boxes =
        new Dictionary<string, BoundingBoxDto>(StringComparer.OrdinalIgnoreCase)
        {
            ["BR"] = new(-33.75, -73.99, 5.27, -28.85),
            ["AR"] = new(-55.06, -73.58, -21.78, -53.59),
            ["US"] = new(24.52, -124.77, 49.38, -66.95),
            ["CA"] = new(41.68, -141.0, 83.11, -52.62),
            ["MX"] = new(14.53, -118.40, 32.72, -86.71),
            ["CL"] = new(-55.98, -109.45, -17.51, -66.42),
            ["CO"] = new(-4.23, -79.0, 12.46, -66.87),
            ["PE"] = new(-18.35, -81.33, -0.04, -68.65),
            ["UY"] = new(-34.98, -58.44, -30.09, -53.09),
            ["PY"] = new(-27.61, -62.65, -19.29, -54.26),
            ["BO"] = new(-22.90, -69.64, -9.68, -57.45),
            ["EC"] = new(-5.01, -81.08, 1.44, -75.19),
            ["VE"] = new(0.65, -73.35, 12.20, -59.80),
            ["GB"] = new(49.86, -8.65, 60.86, 1.77),
            ["DE"] = new(47.27, 5.87, 55.06, 15.04),
            ["FR"] = new(41.33, -5.14, 51.09, 9.56),
            ["ES"] = new(36.0, -9.30, 43.79, 4.33),
            ["IT"] = new(36.65, 6.63, 47.09, 18.52),
            ["PT"] = new(36.96, -9.53, 42.15, -6.19),
            ["NL"] = new(50.75, 3.36, 53.56, 7.21),
            ["PL"] = new(49.0, 14.12, 54.84, 24.15),
            ["UA"] = new(44.39, 22.14, 52.38, 40.23),
            ["RU"] = new(41.19, 19.64, 81.86, 180.0),
            ["TR"] = new(35.82, 25.67, 42.11, 44.82),
            ["CN"] = new(18.16, 73.50, 53.56, 134.77),
            ["JP"] = new(24.25, 122.93, 45.52, 153.99),
            ["IN"] = new(6.75, 68.18, 35.51, 97.40),
            ["AU"] = new(-43.64, 112.92, -10.06, 153.64),
            ["ZA"] = new(-34.84, 16.45, -22.13, 32.89),
            ["EG"] = new(22.0, 24.70, 31.67, 36.87),
            ["IL"] = new(29.50, 34.27, 33.34, 35.88),
            ["SA"] = new(16.35, 34.63, 32.15, 55.67),
            ["AE"] = new(22.63, 51.58, 26.08, 56.38),
            ["KR"] = new(33.11, 124.61, 38.61, 131.87),
            ["ID"] = new(-11.01, 95.01, 6.08, 141.02),
            ["TH"] = new(5.61, 97.34, 20.46, 105.64),
            ["PH"] = new(4.64, 116.95, 21.12, 126.60),
            ["NG"] = new(4.27, 2.69, 13.89, 14.68),
            ["KE"] = new(-4.68, 33.91, 5.03, 41.91)
        };

    public static IReadOnlyList<string> AllCountryCodes => Boxes.Keys.OrderBy(c => c).ToList();

    public static bool TryGet(string countryCode, out BoundingBoxDto bbox)
    {
        if (string.IsNullOrWhiteSpace(countryCode))
        {
            bbox = default!;
            return false;
        }

        return Boxes.TryGetValue(countryCode.ToUpperInvariant(), out bbox!);
    }

    public static BoundingBoxDto Get(string countryCode)
    {
        if (!TryGet(countryCode, out var bbox))
        {
            throw new KeyNotFoundException($"Country bounding box not defined: {countryCode}");
        }

        return bbox;
    }
}
