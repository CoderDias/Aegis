using Aegis.Application.Dtos.Geo;

namespace Aegis.Infrastructure.External.HostDiscovery;

public sealed record ViewportHostContext(
    string CountryCode,
    string? StateRegion,
    BoundingBoxDto FocusBbox,
    BoundingBoxDto VisibleBbox)
{
    public string IngestKey =>
        string.IsNullOrWhiteSpace(StateRegion)
            ? CountryCode
            : $"{CountryCode}:{StateRegion}";
}

public static class ViewportHostFocus
{
    public static ViewportHostContext Create(BoundingBoxDto visible, string countryCode, string? stateRegion) =>
        new(countryCode, stateRegion, BuildFocusBbox(visible), visible);

    public static ViewportHostContext ForCountry(string countryCode, BoundingBoxDto countryBbox) =>
        new(countryCode.ToUpperInvariant(), null, countryBbox, countryBbox);

    public static BoundingBoxDto BuildFocusBbox(BoundingBoxDto viewport, double maxSpanDegrees = 0.35)
    {
        var centerLat = (viewport.South + viewport.North) / 2;
        var centerLng = (viewport.West + viewport.East) / 2;
        var latSpan = viewport.North - viewport.South;
        var lngSpan = viewport.East - viewport.West;
        var halfLat = Math.Clamp(Math.Min(latSpan / 4, maxSpanDegrees / 2), 0.06, maxSpanDegrees / 2);
        var halfLng = Math.Clamp(Math.Min(lngSpan / 4, maxSpanDegrees / 2), 0.06, maxSpanDegrees / 2);

        return new BoundingBoxDto(
            centerLat - halfLat,
            centerLng - halfLng,
            centerLat + halfLat,
            centerLng + halfLng);
    }

    public static bool IsInside(BoundingBoxDto bbox, double lat, double lng) =>
        lat >= bbox.South && lat <= bbox.North && lng >= bbox.West && lng <= bbox.East;

    public static bool MatchesState(string? targetState, string? geoRegion)
    {
        if (string.IsNullOrWhiteSpace(targetState))
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(geoRegion))
        {
            return false;
        }

        return geoRegion.Contains(targetState, StringComparison.OrdinalIgnoreCase) ||
               targetState.Contains(geoRegion, StringComparison.OrdinalIgnoreCase);
    }
}
