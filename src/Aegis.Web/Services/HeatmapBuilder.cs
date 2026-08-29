using Aegis.Application.Abstractions;
using Aegis.Application.Dtos.Geo;
using Aegis.Application.Dtos.Intel;
using Aegis.Application.Geo;
using Aegis.Application.Dtos.Investigations;
using Aegis.Application.Dtos.Flights;
using Aegis.Web.Services;

namespace Aegis.Web.Services;

public static class HeatmapBuilder
{
    public static IReadOnlyList<object> Build(
        WorkspaceState workspace,
        IGeoIntelCache geoIntel,
        FlightSnapshot? flightSnapshot,
        IReadOnlyList<AssetDto> investigationAssets,
        BoundingBoxDto? bbox,
        int zoom)
    {
        var points = new List<object>();

        void Add(double lat, double lng, double weight)
        {
            if (bbox is not null && zoom > 7 &&
                (lat < bbox.South || lat > bbox.North || lng < bbox.West || lng > bbox.East))
            {
                return;
            }

            points.Add(new { lat, lng, weight });
        }

        foreach (var asset in investigationAssets)
        {
            if (asset.Location is not null)
            {
                Add(asset.Location.Lat, asset.Location.Lng, 1.5);
            }
        }

        if (workspace.Layers.Shodan)
        {
            foreach (var host in workspace.ShodanRegionCache.Concat(workspace.ShodanHosts))
            {
                Add(host.Lat, host.Lng, host.HasExploitableVuln ? 2.5 : 1.2);
            }
        }

        if (workspace.Layers.News)
        {
            foreach (var news in workspace.NewsRegionCache)
            {
                if (news.Lat is not null && news.Lng is not null)
                {
                    Add(news.Lat.Value, news.Lng.Value, 1);
                }
            }
        }

        if (workspace.Layers.Ransomware)
        {
            foreach (var victim in workspace.RansomwareVictims)
            {
                if (victim.Lat is not null && victim.Lng is not null)
                {
                    Add(victim.Lat.Value, victim.Lng.Value, 1.8);
                }
            }
        }

        if (workspace.Layers.Seismic)
        {
            foreach (var marker in geoIntel.GetSeismic().Where(m => SeismicDisplayPolicy.IsVisibleOnMap(m)))
            {
                var weight = marker.Weight * SeismicDisplayPolicy.ComputeOpacity(marker);
                if (weight > 0.01)
                {
                    points.Add(new { lat = marker.Lat, lng = marker.Lng, weight });
                }
            }
        }

        if (workspace.Layers.Ships && zoom >= 7)
        {
            foreach (var marker in geoIntel.GetShips())
            {
                Add(marker.Lat, marker.Lng, marker.Weight);
            }
        }

        if (workspace.Layers.Aircraft && flightSnapshot is not null)
        {
            foreach (var aircraft in flightSnapshot.Aircraft)
            {
                Add(aircraft.Lat, aircraft.Lng, aircraft.OnGround ? 0.8 : 1.1);
            }
        }

        foreach (var (lat, lng, weight) in workspace.HeatmapOverlayPoints)
        {
            Add(lat, lng, weight);
        }

        return points;
    }
}
