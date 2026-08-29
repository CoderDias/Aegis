using Aegis.Application.Dtos.Map;

namespace Aegis.Infrastructure.External.Overpass;

public static class MapFeatureLayers
{
    private static readonly HashSet<string> PublicAmenities = new(StringComparer.OrdinalIgnoreCase)
    {
        "school", "university", "college", "hospital", "clinic", "townhall", "library",
        "fire_station", "police", "courthouse", "public_building", "community_centre",
        "place_of_worship", "theatre", "arts_centre", "museum", "embassy", "post_office",
        "social_facility"
    };

    public static bool IsErb(MapFeatureDto feature) =>
        string.Equals(feature.OsmType, "anatel-erb", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(feature.Tags.GetValueOrDefault("source"), "anatel-erb", StringComparison.OrdinalIgnoreCase);

    public static bool IsPublicCamera(MapFeatureDto feature) =>
        string.Equals(feature.OsmType, "brazuca-camera", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(feature.Tags.GetValueOrDefault("source"), "brazuca-camera", StringComparison.OrdinalIgnoreCase);

    public static bool IsPort(MapFeatureDto feature) =>
        string.Equals(feature.OsmType, "brazuca-port", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(feature.Tags.GetValueOrDefault("source"), "brazuca-port", StringComparison.OrdinalIgnoreCase);

    public static bool IsBuilding(MapFeatureDto feature) =>
        !IsRadioTower(feature) &&
        !IsRepeater(feature) &&
        !IsErb(feature) &&
        !IsPublicCamera(feature) &&
        !IsPort(feature) &&
        !string.Equals(feature.OsmType, "static", StringComparison.OrdinalIgnoreCase) &&
        (feature.Tags.ContainsKey("building") ||
        feature.Tags.ContainsKey("building:part") ||
        feature.Tags.ContainsKey("man_made") ||
        (feature.Tags.TryGetValue("amenity", out var amenity) && PublicAmenities.Contains(amenity)) ||
        string.Equals(feature.Tags.GetValueOrDefault("office"), "government", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(feature.Category, "yes", StringComparison.OrdinalIgnoreCase) ||
        (feature.Category?.StartsWith("building", StringComparison.OrdinalIgnoreCase) ?? false));

    public static bool IsRoad(MapFeatureDto feature) =>
        feature.Tags.ContainsKey("highway") ||
        (feature.Category?.Length > 0 &&
         !IsBuilding(feature) &&
         HighwayCategories.Contains(feature.Category));

    public static bool IsPoi(MapFeatureDto feature)
    {
        if (string.Equals(feature.OsmType, "static", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (IsBuilding(feature) || IsRoad(feature))
        {
            return false;
        }

        if (feature.Tags.ContainsKey("military"))
        {
            return true;
        }

        if (string.Equals(feature.Tags.GetValueOrDefault("landuse"), "military", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (feature.Tags.TryGetValue("amenity", out var amenity) && GovernmentPoiAmenities.Contains(amenity))
        {
            return true;
        }

        return string.Equals(feature.Tags.GetValueOrDefault("office"), "government", StringComparison.OrdinalIgnoreCase);
    }

    private static readonly HashSet<string> GovernmentPoiAmenities = new(StringComparer.OrdinalIgnoreCase)
    {
        "townhall", "school", "university", "college", "public_building", "courthouse",
        "embassy", "police", "fire_station", "community_centre", "post_office",
        "social_facility", "library", "hospital", "clinic"
    };

    public static bool IsRepeater(MapFeatureDto feature) =>
        string.Equals(feature.OsmType, "repeaterbook", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(feature.Tags.GetValueOrDefault("source"), "repeaterbook", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(feature.Tags.GetValueOrDefault("communication:amateur_radio"), "repeater", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(feature.Tags.GetValueOrDefault("amateur_radio"), "repeater", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(feature.Tags.GetValueOrDefault("service"), "amateur_radio", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(feature.Tags.GetValueOrDefault("tower:type"), "amateur_radio", StringComparison.OrdinalIgnoreCase);

    public static bool IsRadioTower(MapFeatureDto feature)
    {
        if (IsRepeater(feature))
        {
            return false;
        }

        if (feature.Tags.TryGetValue("man_made", out var manMade))
        {
            return manMade is "mast" or "tower" or "communications_tower" or "antenna";
        }

        if (feature.Tags.TryGetValue("tower:type", out var towerType))
        {
            return towerType is "communication" or "broadcast" or "observation";
        }

        return false;
    }

    public static string ResolveKind(MapFeatureDto feature)
    {
        if (string.Equals(feature.OsmType, "static", StringComparison.OrdinalIgnoreCase))
        {
            return "poi";
        }

        if (IsRoad(feature))
        {
            return "road";
        }

        if (IsRepeater(feature))
        {
            return "repeater";
        }

        if (IsErb(feature))
        {
            return "erb";
        }

        if (IsPublicCamera(feature))
        {
            return "public_camera";
        }

        if (IsPort(feature))
        {
            return "port";
        }

        if (IsRadioTower(feature))
        {
            return "radio_tower";
        }

        if (IsBuilding(feature))
        {
            return IsPublicBuilding(feature) ? "public_building" : "building";
        }

        if (IsPoi(feature))
        {
            return "poi";
        }

        return "other";
    }

    public static bool IsPublicBuilding(MapFeatureDto feature) =>
        (feature.Tags.TryGetValue("amenity", out var amenity) && PublicAmenities.Contains(amenity)) ||
        string.Equals(feature.Tags.GetValueOrDefault("office"), "government", StringComparison.OrdinalIgnoreCase);

    public static bool ShouldRenderAsPolygon(IReadOnlyDictionary<string, string> tags, int coordinateCount, string osmType)
    {
        if (coordinateCount < 3 || osmType is not ("way" or "relation"))
        {
            return false;
        }

        if (tags.ContainsKey("highway"))
        {
            return false;
        }

        if (tags.ContainsKey("building") || tags.ContainsKey("building:part"))
        {
            return true;
        }

        if (tags.TryGetValue("amenity", out var amenity) && PublicAmenities.Contains(amenity))
        {
            return true;
        }

        if (string.Equals(tags.GetValueOrDefault("office"), "government", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return tags.ContainsKey("man_made") || tags.ContainsKey("landuse");
    }

    private static readonly HashSet<string> HighwayCategories = new(StringComparer.OrdinalIgnoreCase)
    {
        "motorway", "trunk", "primary", "secondary", "tertiary",
        "unclassified", "residential", "living_street", "service",
        "motorway_link", "trunk_link", "primary_link", "secondary_link", "tertiary_link",
        "track", "road", "pedestrian"
    };
}

public enum OverpassLayerKind
{
    Buildings,
    Poi,
    Roads,
    RadioTowers,
    Repeaters,
    OsmVessels
}
