using System.Collections.Concurrent;
using Aegis.Application.Abstractions;
using Aegis.Application.Dtos.Map;
using Aegis.Domain.ValueObjects;

namespace Aegis.Infrastructure;

public sealed class ViewportBroker : IViewportBroker
{
    private readonly ConcurrentDictionary<string, byte> _viewers = new(StringComparer.Ordinal);
    private readonly object _lock = new();
    private Viewport? _last;
    private MapLayerState? _activeLayers;

    public Viewport? Last
    {
        get
        {
            lock (_lock)
            {
                return _last;
            }
        }
    }

    public MapLayerState? ActiveLayers
    {
        get
        {
            lock (_lock)
            {
                return _activeLayers;
            }
        }
    }

    public bool HasActiveViewers => !_viewers.IsEmpty;

    public void Report(BoundingBox box, ZoomLevel zoom, MapLayerState? layers = null)
    {
        ArgumentNullException.ThrowIfNull(box);
        ArgumentNullException.ThrowIfNull(zoom);

        lock (_lock)
        {
            _last = new Viewport(box, zoom, DateTimeOffset.UtcNow, layers);
            if (layers is not null)
            {
                _activeLayers = layers;
            }
        }
    }

    public void ViewerConnected(string circuitId)
    {
        if (string.IsNullOrWhiteSpace(circuitId))
        {
            return;
        }

        _viewers.TryAdd(circuitId, 0);
    }

    public void ViewerDisconnected(string circuitId)
    {
        if (string.IsNullOrWhiteSpace(circuitId))
        {
            return;
        }

        _viewers.TryRemove(circuitId, out _);
    }
}
