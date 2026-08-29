namespace Aegis.Infrastructure.Data.Entities;

public sealed class DiscoveredHostEntity
{
    public string Ip { get; set; } = string.Empty;

    public string CountryCode { get; set; } = string.Empty;

    public double? Lat { get; set; }

    public double? Lng { get; set; }

    public string? City { get; set; }

    public string? Country { get; set; }

    public string? Org { get; set; }

    public string? Product { get; set; }

    public int? Port { get; set; }

    public string? Transport { get; set; }

    /// <summary>Censys, InternetDb, Probe, IpApi</summary>
    public string Source { get; set; } = "Probe";

    public bool? IsUp { get; set; }

    public DateTimeOffset? LastProbeAt { get; set; }

    public DateTimeOffset? CensysFetchedAt { get; set; }

    public string? VulnerabilitiesJson { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class CensysApiUsageEntity
{
    public string MonthKey { get; set; } = string.Empty;

    public int QueryCount { get; set; }
}

public sealed class CountryIngestStateEntity
{
    public string CountryCode { get; set; } = string.Empty;

    public int CidrCursor { get; set; }

    public string? SearchPageToken { get; set; }

    public bool SearchComplete { get; set; }

    public int OverpassTileIndex { get; set; }

    public bool OverpassWarmComplete { get; set; }

    public int ShodanRegionIndex { get; set; }

    public bool ShodanWarmComplete { get; set; }

    public bool PrefetchWarmComplete { get; set; }

    public DateTimeOffset? LastPrefetchUtc { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
