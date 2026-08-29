using Aegis.Application.Abstractions;
using Aegis.Application.Dtos.Geo;
using Aegis.Application.Dtos.Intel;
using Aegis.Application.Dtos.Map;
using Aegis.Application.Geo;

namespace Aegis.Application.Services;

public sealed class ViewportQueryService(
    IMapFeatureService mapFeatures,
    IShodanDeviceService shodan,
    IRssFeedStore newsStore)
{
    public async Task<ViewportQueryResult> QueryAsync(
        ViewportQueryRequest request,
        CancellationToken cancellationToken = default)
    {
        string? hint = null;
        IReadOnlyList<MapFeatureDto> features = [];
        var wantsFeatures = request.Layers.Buildings ||
                            request.Layers.AreasOfInterest ||
                            request.Layers.Roads ||
                            request.Layers.RadioTowers ||
                            request.Layers.Repeaters ||
                            request.Layers.Erbs ||
                            request.Layers.PublicCameras ||
                            request.Layers.Ports;

        if (wantsFeatures)
        {
            features = await mapFeatures
                .GetFeaturesAsync(request.Bbox, request.Zoom, request.Layers, cancellationToken)
                .ConfigureAwait(false);

            if (features.Count == 0)
            {
                hint = "Nenhum dado cartográfico nesta área para as camadas ativas.";
            }
        }

        IReadOnlyList<ShodanHostDto> shodanHosts = [];
        if (request.Layers.Shodan)
        {
            if (shodan.IsConfigured)
            {
                shodanHosts = await shodan
                    .SearchInViewportAsync(request.Bbox, request.Zoom, cancellationToken)
                    .ConfigureAwait(false);

                if (!string.IsNullOrEmpty(shodan.LastSearchMessage))
                {
                    hint = shodan.LastSearchMessage;
                }
            }
            else
            {
                hint ??= "Configure Shodan:ApiKey e ative a camada Shodan.";
            }
        }

        IReadOnlyList<NewsItemDto> newsItems = [];
        if (request.Layers.News)
        {
            newsItems = await newsStore
                .ListGeolocatedNewsAsync(cancellationToken)
                .ConfigureAwait(false);
        }

        return new ViewportQueryResult([], features, shodanHosts, newsItems, hint);
    }

    public Task<IReadOnlyList<NewsItemDto>> GetGeolocatedNewsAsync(CancellationToken cancellationToken = default) =>
        newsStore.ListGeolocatedNewsAsync(cancellationToken);

    public static string ComputeRegionHash(BoundingBoxDto bbox, int zoom, MapLayerState layers)
    {
        var fetchZoom = ViewportTileGrid.FetchZoomLevel(zoom);
        var tileHash = ViewportTileGrid.ComputeRegionHash(bbox, zoom);
        return $"{tileHash}|b:{layers.Buildings}|p:{layers.AreasOfInterest}|r:{layers.Roads}|rt:{layers.RadioTowers}|rp:{layers.Repeaters}|erb:{layers.Erbs}|cam:{layers.PublicCameras}|port:{layers.Ports}";
    }

    public static string ComputeRegionHash(BoundingBoxDto bbox, int zoom) =>
        ViewportTileGrid.ComputeRegionHash(bbox, zoom);
}
