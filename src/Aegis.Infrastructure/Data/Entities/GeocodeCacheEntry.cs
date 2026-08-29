namespace Aegis.Infrastructure.Data.Entities;

public enum GeocodeCacheKind
{
    Forward = 0,
    Reverse = 1
}

public sealed class GeocodeCacheEntry
{
    public long Id { get; set; }

    public string QueryHash { get; set; } = string.Empty;

    public GeocodeCacheKind Kind { get; set; }

    public string RequestJson { get; set; } = string.Empty;

    public string ResponseJson { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset ExpiresAt { get; set; }
}
