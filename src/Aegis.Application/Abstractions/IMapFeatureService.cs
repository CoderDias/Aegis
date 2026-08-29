using Aegis.Application.Dtos.Geo;
using Aegis.Application.Dtos.Map;

namespace Aegis.Application.Abstractions;

public interface IMapFeatureService
{
    Task<IReadOnlyList<MapFeatureDto>> GetFeaturesAsync(
        BoundingBoxDto bbox,
        int zoom,
        MapLayerState layers,
        CancellationToken cancellationToken = default);
}
