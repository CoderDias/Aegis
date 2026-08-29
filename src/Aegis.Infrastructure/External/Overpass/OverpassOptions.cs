namespace Aegis.Infrastructure.External.Overpass;

public sealed class OverpassOptions
{
    public const string SectionName = "Overpass";

    public string BaseUrl { get; set; } = "https://overpass.kumi.systems/api/interpreter";

    public string FallbackBaseUrl { get; set; } = "https://overpass-api.de/api/interpreter";

    public int MinZoom { get; set; } = 4;

    public double MaxBboxAreaDeg2 { get; set; } = 0.25;

    public int MaxFeatures { get; set; } = 2000;

    public int RequestTimeoutSeconds { get; set; } = 12;

    public int MaxConcurrentRequests { get; set; } = 4;

    public int MinRequestIntervalMs { get; set; } = 200;
}
