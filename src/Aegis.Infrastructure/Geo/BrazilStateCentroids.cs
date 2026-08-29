using Aegis.Application.Dtos.Geo;

namespace Aegis.Infrastructure.Geo;

internal static class BrazilStateCentroids
{
    private static readonly Dictionary<string, CoordinateDto> ByUf = new(StringComparer.OrdinalIgnoreCase)
    {
        ["AC"] = new(-9.9749, -67.8243),
        ["AL"] = new(-9.6658, -35.7350),
        ["AM"] = new(-3.1190, -60.0217),
        ["AP"] = new(0.0349, -51.0694),
        ["BA"] = new(-12.9714, -38.5014),
        ["CE"] = new(-3.7319, -38.5267),
        ["DF"] = new(-15.7942, -47.8822),
        ["ES"] = new(-20.3155, -40.3128),
        ["GO"] = new(-16.6869, -49.2648),
        ["MA"] = new(-2.5387, -44.2825),
        ["MG"] = new(-19.9167, -43.9345),
        ["MS"] = new(-20.4697, -54.6201),
        ["MT"] = new(-15.6014, -56.0979),
        ["PA"] = new(-1.4554, -48.4898),
        ["PB"] = new(-7.1195, -34.8450),
        ["PE"] = new(-8.0476, -34.8770),
        ["PI"] = new(-5.0892, -42.8019),
        ["PR"] = new(-25.4284, -49.2733),
        ["RJ"] = new(-22.9068, -43.1729),
        ["RN"] = new(-5.7945, -35.2110),
        ["RO"] = new(-8.7619, -63.9039),
        ["RR"] = new(2.8235, -60.6758),
        ["RS"] = new(-30.0346, -51.2177),
        ["SC"] = new(-27.5954, -48.5480),
        ["SE"] = new(-10.9472, -37.0731),
        ["SP"] = new(-23.5505, -46.6333),
        ["TO"] = new(-10.1840, -48.3336)
    };

    public static CoordinateDto Resolve(string? uf, string stableKey)
    {
        var baseCoord = !string.IsNullOrWhiteSpace(uf) && ByUf.TryGetValue(uf, out var known)
            ? known
            : new CoordinateDto(-14.2350, -51.9253);

        return Offset(baseCoord, stableKey);
    }

    public static string? InferUfFromUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return null;
        }

        var lower = url.ToLowerInvariant();

        if (lower.Contains("cetsp") || lower.Contains("der.sp") || lower.Contains("prefeitura.sp") ||
            lower.Contains("rodovias-der-sp") || lower.Contains("citycameras.prefeitura.sp"))
        {
            return "SP";
        }

        if (lower.Contains("parana") || lower.Contains("viapar") || lower.Contains(".pr.gov") || lower.Contains("-pr/"))
        {
            return "PR";
        }

        if (lower.Contains("rio-grande") || lower.Contains("daer-rs") || lower.Contains(".rs.gov"))
        {
            return "RS";
        }

        if (lower.Contains("minas") || lower.Contains(".mg.gov"))
        {
            return "MG";
        }

        if (lower.Contains("santa-catarina") || lower.Contains(".sc.gov"))
        {
            return "SC";
        }

        if (lower.Contains("bahia") || lower.Contains(".ba.gov"))
        {
            return "BA";
        }

        if (lower.Contains("pernambuco") || lower.Contains(".pe.gov"))
        {
            return "PE";
        }

        if (lower.Contains("ceara") || lower.Contains(".ce.gov"))
        {
            return "CE";
        }

        if (lower.Contains("goias") || lower.Contains(".go.gov"))
        {
            return "GO";
        }

        if (lower.Contains("distrito-federal") || lower.Contains("brasilia"))
        {
            return "DF";
        }

        return null;
    }

    private static CoordinateDto Offset(CoordinateDto baseCoord, string stableKey)
    {
        var hash = Math.Abs(StringComparer.Ordinal.GetHashCode(stableKey));
        var latOffset = ((hash % 1000) / 1000.0 - 0.5) * 0.35;
        var lngOffset = (((hash / 1000) % 1000) / 1000.0 - 0.5) * 0.35;
        return new CoordinateDto(baseCoord.Lat + latOffset, baseCoord.Lng + lngOffset);
    }
}
