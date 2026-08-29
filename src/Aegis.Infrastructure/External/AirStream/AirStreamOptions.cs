namespace Aegis.Infrastructure.External.AirStream;

public sealed class AirStreamOptions
{
    public const string SectionName = "AirStream";

    public string BaseUrl { get; set; } = "https://opendata.adsb.fi/api";

    public string ApiToken { get; set; } = string.Empty;

    public int RadiusNm { get; set; } = 50;

    public bool Enabled { get; set; } = true;
}
