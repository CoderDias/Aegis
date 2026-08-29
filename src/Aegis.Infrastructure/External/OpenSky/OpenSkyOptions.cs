namespace Aegis.Infrastructure.External.OpenSky;

public sealed class OpenSkyOptions
{
    public const string SectionName = "OpenSky";

    public string BaseUrl { get; set; } = "https://opensky-network.org/api";

    public string ClientId { get; set; } = string.Empty;

    public string ClientSecret { get; set; } = string.Empty;

    public int PollingIntervalSeconds { get; set; } = 15;

    public int RateLimitPerMinute { get; set; } = 6;

    public int OnDemandTimeoutSeconds { get; set; } = 10;
}
