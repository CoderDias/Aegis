namespace Aegis.Infrastructure.External.Nominatim;

public sealed class NominatimOptions
{
    public const string SectionName = "Nominatim";

    public string BaseUrl { get; set; } = "https://nominatim.openstreetmap.org";

    public string UserAgent { get; set; } = "Aegis-OSINT/1.0 (local; contact: none)";

    public int CacheDays { get; set; } = 7;

    public int MinSearchLength { get; set; } = 3;

    public int MaxResults { get; set; } = 5;
}
