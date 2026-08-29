using System.Text.Json;
using Aegis.Application.Dtos.Investigations;
using Aegis.Application.Services;
using Aegis.Domain.Enums;

namespace Aegis.Application.Osint;

public sealed class InvestigationExportService
{
    public InvestigationExportDocument Build(
        InvestigationDetailDto investigation,
        IReadOnlyList<OsintCorrelationHit> correlations,
        double? viewportSouth = null,
        double? viewportWest = null,
        double? viewportNorth = null,
        double? viewportEast = null,
        int? zoom = null)
    {
        var osintEvents = investigation.Timeline
            .Where(e => e.Type == TimelineEventType.OsintSourceAccess)
            .Select(e => new InvestigationExportOsintAccess(
                e.OccurredAt,
                e.Message,
                e.PayloadJson))
            .ToList();

        var assets = investigation.Assets
            .Select(a => new InvestigationExportAsset(
                a.Id,
                a.Type.ToString(),
                a.DisplayName,
                a.Source.ToString(),
                a.Location?.Lat,
                a.Location?.Lng,
                a.Notes))
            .ToList();

        return new InvestigationExportDocument(
            ExportedAt: DateTimeOffset.UtcNow,
            Investigation: new InvestigationExportHeader(
                investigation.Id,
                investigation.Title,
                investigation.Description,
                investigation.Status.ToString(),
                investigation.CreatedAt,
                investigation.UpdatedAt),
            Viewport: viewportSouth is not null && viewportWest is not null && viewportNorth is not null && viewportEast is not null
                ? new InvestigationExportViewport(viewportSouth.Value, viewportWest.Value, viewportNorth.Value, viewportEast.Value, zoom)
                : null,
            Assets: assets,
            Geofences: investigation.Geofences,
            Timeline: investigation.Timeline,
            OsintSourceAccesses: osintEvents,
            Correlations: correlations,
            Source: "Aegis OSINT Brazuca export v1");
    }

    public string ToJson(InvestigationExportDocument document, bool indented = true) =>
        JsonSerializer.Serialize(document, new JsonSerializerOptions
        {
            WriteIndented = indented,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
}

public sealed record InvestigationExportDocument(
    DateTimeOffset ExportedAt,
    InvestigationExportHeader Investigation,
    InvestigationExportViewport? Viewport,
    IReadOnlyList<InvestigationExportAsset> Assets,
    IReadOnlyList<GeofenceDto> Geofences,
    IReadOnlyList<TimelineEventDto> Timeline,
    IReadOnlyList<InvestigationExportOsintAccess> OsintSourceAccesses,
    IReadOnlyList<OsintCorrelationHit> Correlations,
    string Source);

public sealed record InvestigationExportHeader(
    Guid Id,
    string Title,
    string? Description,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record InvestigationExportViewport(
    double South,
    double West,
    double North,
    double East,
    int? Zoom);

public sealed record InvestigationExportAsset(
    Guid Id,
    string Type,
    string DisplayName,
    string Source,
    double? Lat,
    double? Lng,
    string? Notes);

public sealed record InvestigationExportOsintAccess(
    DateTimeOffset OccurredAt,
    string Message,
    string? PayloadJson);
