namespace Aegis.Infrastructure.External.GeoIntel;

public sealed class GeoIntelOptions
{
    public const string SectionName = "GeoIntel";

    public bool Enabled { get; set; } = true;

    public string? AisHubUsername { get; set; }

    public string? AisStreamApiKey { get; set; }

    public bool UseAisStream => !string.IsNullOrWhiteSpace(AisStreamApiKey);

    public double SeismicMinMagnitude { get; set; } = 2.5;

    public int PollIntervalMinutes { get; set; } = 5;

    public int SeismicCacheMinutes { get; set; } = 15;

    public int ShipsCacheMinutes { get; set; } = 5;

    public int WeatherAlertsCacheMinutes { get; set; } = 10;

    public int MaxShipMarkers { get; set; } = 2000;

    public bool UseOsmShipFallback { get; set; } = true;
}
