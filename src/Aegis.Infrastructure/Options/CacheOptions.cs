namespace Aegis.Infrastructure.Options;

public sealed class CacheOptions
{
    public const string SectionName = "Cache";

    public int FlightDataTtlMinutes { get; set; } = 2;

    public int GeocodeTtlDays { get; set; } = 7;

    public int OverpassTtlSeconds { get; set; } = 300;

    public int ShodanTileTtlMinutes { get; set; } = 30;

    public int ShodanRegionTtlHours { get; set; } = 24;
}
