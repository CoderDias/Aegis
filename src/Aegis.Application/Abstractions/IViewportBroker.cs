using Aegis.Application.Dtos.Map;
using Aegis.Domain.ValueObjects;

namespace Aegis.Application.Abstractions;

public record Viewport(BoundingBox Box, ZoomLevel Zoom, DateTimeOffset ReportedAt, MapLayerState? Layers);

public interface IViewportBroker
{
    void Report(BoundingBox box, ZoomLevel zoom, MapLayerState? layers = null);

    Viewport? Last { get; }

    MapLayerState? ActiveLayers { get; }

    bool HasActiveViewers { get; }

    void ViewerConnected(string circuitId);

    void ViewerDisconnected(string circuitId);
}
