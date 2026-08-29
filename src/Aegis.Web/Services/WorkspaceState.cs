using Aegis.Application.Dtos.Flights;
using Aegis.Application.Dtos.Geo;
using Aegis.Application.Dtos.Intel;
using Aegis.Application.Dtos.Map;

namespace Aegis.Web.Services;

public sealed class LayerState
{
    public bool Aircraft { get; set; } = true;
    public bool Buildings { get; set; } = true;
    public bool Roads { get; set; } = true;
    public bool AreasOfInterest { get; set; } = true;
    public bool Heatmap { get; set; }
    public bool Shodan { get; set; } = true;
    public bool News { get; set; } = true;
    public bool Ransomware { get; set; } = true;
    public bool Ships { get; set; } = true;
    public bool Seismic { get; set; } = true;
    public bool RadioTowers { get; set; } = true;
    public bool Repeaters { get; set; } = true;
    public bool Erbs { get; set; }
    public bool PublicCameras { get; set; }
    public bool Ports { get; set; } = true;
    public bool WeatherAlerts { get; set; } = true;
    public bool InpeOverlay { get; set; }

    public MapLayerState ToDto() => new(
        Aircraft, Buildings, Roads, AreasOfInterest, Heatmap, Shodan, News, Ransomware,
        Ships, Seismic, RadioTowers, Repeaters, Erbs, PublicCameras, Ports, WeatherAlerts, InpeOverlay);
}

public enum InspectorTab
{
    Case,
    Flight,
    News,
    Ransomware,
    Hosts,
    Osint
}

public sealed class SelectionState
{
    public string? Kind { get; set; }
    public string? Id { get; set; }
    public object? Payload { get; set; }

    public void Clear()
    {
        Kind = null;
        Id = null;
        Payload = null;
    }

    public bool IsEmpty => string.IsNullOrEmpty(Kind);
}

public enum DrawPurpose
{
    None,
    SaveAnnotation,
    InvestigationGeofence
}

public sealed class WorkspaceState
{
    public Guid? ActiveInvestigationId { get; set; }
    public string? ActiveInvestigationTitle { get; set; }
    public LayerState Layers { get; } = new();
    public SelectionState Selection { get; } = new();
    public FlightFilterDto FlightFilters { get; set; } = new();
    public BoundingBoxDto? Viewport { get; set; }
    public int Zoom { get; set; } = 5;
    public InspectorTab ActiveTab { get; set; } = InspectorTab.Case;
    public IReadOnlyList<ShodanHostDto> ShodanHosts { get; set; } = [];
    public IReadOnlyList<ShodanHostDto> ShodanRegionCache { get; set; } = [];
    public IReadOnlyList<NewsItemDto> NewsRegionCache { get; set; } = [];
    public IReadOnlyList<RansomwareVictimDto> RansomwareVictims { get; set; } = [];
    public IReadOnlyList<(double Lat, double Lng, double Weight)> HeatmapOverlayPoints { get; set; } = [];
    public DrawPurpose DrawPurpose { get; set; }
    public string? PendingInvestigationTitle { get; set; }
    public string? PendingDrawKind { get; set; }
    public string? PendingDrawGeometryJson { get; set; }
    public double? PendingCircleCenterLat { get; set; }
    public double? PendingCircleCenterLng { get; set; }
    public bool DrawModeActive { get; set; }
    public string? DrawKind { get; set; }
    public int DrawPointCount { get; set; }
    public bool TimelineExpanded { get; set; } = true;
    public bool AlertsPanelOpen { get; set; }

    public event Action? Changed;
    public event Action? ViewportChanged;
    public event Action? HeatmapOverlayChanged;
    public event Action? StartGeofenceDrawRequested;

    public void NotifyChanged() => Changed?.Invoke();

    public void NotifyHeatmapOverlayChanged() => HeatmapOverlayChanged?.Invoke();

    public void OpenAlertsPanel()
    {
        AlertsPanelOpen = true;
        NotifyChanged();
    }

    public void CloseAlertsPanel()
    {
        AlertsPanelOpen = false;
        NotifyChanged();
    }

    public void SetViewport(BoundingBoxDto bbox, int zoom)
    {
        Viewport = bbox;
        Zoom = zoom;
        ViewportChanged?.Invoke();
    }

    public void BeginInvestigationGeofenceDraw(string title)
    {
        PendingInvestigationTitle = title.Trim();
        DrawPurpose = DrawPurpose.InvestigationGeofence;
        StartGeofenceDrawRequested?.Invoke();
        NotifyChanged();
    }

    public void BeginAnnotationDraw()
    {
        DrawPurpose = DrawPurpose.SaveAnnotation;
        NotifyChanged();
    }

    public void ClearDrawPurpose()
    {
        DrawPurpose = DrawPurpose.None;
        PendingInvestigationTitle = null;
        PendingDrawKind = null;
        PendingDrawGeometryJson = null;
        PendingCircleCenterLat = null;
        PendingCircleCenterLng = null;
        DrawPointCount = 0;
    }

    public void ClearPendingCircleCenter()
    {
        PendingCircleCenterLat = null;
        PendingCircleCenterLng = null;
    }

    public void ResetDrawSession()
    {
        DrawModeActive = false;
        DrawKind = null;
        DrawPointCount = 0;
        PendingDrawKind = null;
        PendingDrawGeometryJson = null;
        ClearPendingCircleCenter();
    }

    public void Select(string kind, string id, object? payload = null)
    {
        Selection.Kind = kind;
        Selection.Id = id;
        Selection.Payload = payload;
        ActiveTab = kind switch
        {
            "aircraft" => InspectorTab.Flight,
            "shodan" => InspectorTab.Hosts,
            _ => ActiveTab
        };
        NotifyChanged();
    }

    public void ClearSelection()
    {
        Selection.Clear();
        NotifyChanged();
    }
}
