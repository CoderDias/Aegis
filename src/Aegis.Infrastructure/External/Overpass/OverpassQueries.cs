using System.Globalization;

namespace Aegis.Infrastructure.External.Overpass;

public static class OverpassQueries
{
    public static string BuildQuery(
        double south,
        double west,
        double north,
        double east,
        int zoom,
        int maxFeatures,
        OverpassLayerKind layer)
    {
        var limit = Math.Clamp(maxFeatures, 1, 3000);
        var bbox = FormattableString.Invariant($"{south},{west},{north},{east}");

        return layer switch
        {
            OverpassLayerKind.Buildings when zoom >= 6 => FormattableString.Invariant($"""
                [out:json][timeout:25];
                (
                  way["building"]({bbox});
                  relation["type"="building"]({bbox});
                  way["building:part"]({bbox});
                  way["man_made"~"^(works|tower|water_tower|bridge|pier|breakwater|mast|storage_tank|silos)$"]({bbox});
                  nwr["amenity"~"^(school|university|college|hospital|clinic|townhall|library|fire_station|police|courthouse|public_building|community_centre|place_of_worship|theatre|arts_centre|museum|embassy|post_office|social_facility)$"]({bbox});
                  nwr["office"="government"]({bbox});
                );
                out geom {limit};
                """),
            OverpassLayerKind.Buildings when zoom >= 4 => FormattableString.Invariant($"""
                [out:json][timeout:20];
                (
                  nwr["amenity"~"^(townhall|public_building|courthouse|embassy)$"]({bbox});
                  nwr["office"="government"]({bbox});
                  way["building"="public"]({bbox});
                );
                out center {Math.Min(limit, 120)};
                """),
            OverpassLayerKind.Roads when zoom >= 8 => FormattableString.Invariant($"""
                [out:json][timeout:20];
                (
                  way["highway"]["highway"!~"^(footway|path|steps|corridor|bridleway|cycleway|bus_guideway|construction|proposed|raceway|elevator|platform)$"]({bbox});
                );
                out geom {limit};
                """),
            OverpassLayerKind.Roads when zoom >= 5 => FormattableString.Invariant($"""
                [out:json][timeout:15];
                (
                  way["highway"~"^(motorway|trunk|primary|secondary|tertiary|motorway_link|trunk_link|primary_link|secondary_link|tertiary_link)$"]({bbox});
                );
                out geom {Math.Min(limit, 250)};
                """),
            OverpassLayerKind.Poi when zoom >= 8 => FormattableString.Invariant($"""
                [out:json][timeout:20];
                (
                  nwr["amenity"~"^(townhall|school|university|college|hospital|clinic|police|fire_station|courthouse|embassy|library|public_building|community_centre|post_office|social_facility)$"]({bbox});
                  nwr["office"="government"]({bbox});
                  nwr["military"]({bbox});
                  nwr["landuse"="military"]({bbox});
                );
                out center {Math.Min(limit, 200)};
                """),
            OverpassLayerKind.RadioTowers when zoom >= 7 => FormattableString.Invariant($"""
                [out:json][timeout:22];
                (
                  nwr["man_made"~"^(mast|tower|communications_tower|antenna)$"]({bbox});
                  nwr["tower:type"~"^(communication|broadcast|observation)$"]({bbox});
                  nwr["man_made"="antenna"]({bbox});
                  nwr["communication:mobile"="mast"]({bbox});
                  nwr["communication:radio"="mast"]({bbox});
                );
                out center {Math.Min(limit, 800)};
                """),
            OverpassLayerKind.Repeaters when zoom >= 9 => FormattableString.Invariant($"""
                [out:json][timeout:20];
                (
                  nwr["communication:amateur_radio"="repeater"]({bbox});
                  nwr["amateur_radio"="repeater"]({bbox});
                  nwr["service"="amateur_radio"]({bbox});
                  nwr["tower:type"="amateur_radio"]({bbox});
                );
                out center {Math.Min(limit, 400)};
                """),
            OverpassLayerKind.OsmVessels when zoom >= 6 => FormattableString.Invariant($"""
                [out:json][timeout:20];
                (
                  nwr["route"="ferry"]({bbox});
                  nwr["seamark:type"~"^(harbour|berth|mooring|pontoon|ferry_terminal)$"]({bbox});
                  nwr["landuse"="port"]({bbox});
                  nwr["harbour"]({bbox});
                  node["type"="ship"]({bbox});
                  node["seamark:type"="light_vessel"]({bbox});
                );
                out center {Math.Min(limit, 600)};
                """),
            _ => string.Empty
        };
    }

    public static double ComputeBboxAreaDeg2(double south, double west, double north, double east) =>
        Math.Abs(north - south) * Math.Abs(east - west);
}
