using Aegis.Domain.Enums;

namespace Aegis.Application.Dtos.Investigations;

public record InvestigationDetailDto(
    Guid Id,
    string Title,
    string? Description,
    InvestigationStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? ClosedAt,
    IReadOnlyList<AssetDto> Assets,
    IReadOnlyList<AnnotationDto> Annotations,
    IReadOnlyList<TimelineEventDto> Timeline,
    IReadOnlyList<GeofenceDto> Geofences);
