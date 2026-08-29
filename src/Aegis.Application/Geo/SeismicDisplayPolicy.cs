using Aegis.Application.Dtos.Intel;

namespace Aegis.Application.Geo;

public static class SeismicDisplayPolicy
{
    public static readonly TimeSpan MaxMapAge = TimeSpan.FromDays(4);

    public static readonly TimeSpan MaxCacheAge = TimeSpan.FromDays(30);

    public static bool IsVisibleOnMap(GeoMarkerDto marker, DateTimeOffset? now = null)
    {
        now ??= DateTimeOffset.UtcNow;
        if (marker.Timestamp is null)
        {
            return true;
        }

        return now.Value - marker.Timestamp.Value <= MaxMapAge;
    }

    public static double ComputeOpacity(GeoMarkerDto marker, DateTimeOffset? now = null)
    {
        now ??= DateTimeOffset.UtcNow;
        if (marker.Timestamp is null)
        {
            return 1.0;
        }

        var age = now.Value - marker.Timestamp.Value;
        if (age >= MaxMapAge)
        {
            return 0;
        }

        if (age <= TimeSpan.Zero)
        {
            return 1.0;
        }

        return 1.0 - age.TotalDays / MaxMapAge.TotalDays;
    }

    public static bool KeepInCache(GeoMarkerDto marker, DateTimeOffset? now = null)
    {
        now ??= DateTimeOffset.UtcNow;
        if (marker.Timestamp is null)
        {
            return true;
        }

        return now.Value - marker.Timestamp.Value <= MaxCacheAge;
    }
}
