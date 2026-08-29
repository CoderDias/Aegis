namespace Aegis.Infrastructure.Background;

public sealed class RegionalPrefetchOptions
{
    public const string SectionName = "RegionalPrefetch";

    public bool Enabled { get; set; } = true;

    /// <summary>Países vazios = todos do catálogo.</summary>
    public string[] CountryCodes { get; set; } = [];

    public int OverpassFetchZoom { get; set; } = 8;

    public int OverpassTilesPerBatch { get; set; } = 2;

    public int HostIngestBatchesPerCycle { get; set; } = 1;

    public int ShodanRegionsPerBatch { get; set; } = 1;

    public int BatchDelayMs { get; set; } = 2500;

    public int IdleDelayMs { get; set; } = 8000;

    public int RefreshIntervalHours { get; set; } = 12;
}
