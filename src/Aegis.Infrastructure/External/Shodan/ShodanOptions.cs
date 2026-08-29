namespace Aegis.Infrastructure.External.Shodan;

public sealed class ShodanOptions
{
    public const string SectionName = "Shodan";

    public string ApiKey { get; set; } = string.Empty;

    public int MaxResults { get; set; } = 100;

    public bool Enabled { get; set; }
}
