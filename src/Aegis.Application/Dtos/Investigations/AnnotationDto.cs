using Aegis.Domain.Enums;

namespace Aegis.Application.Dtos.Investigations;

public record AnnotationDto(
    Guid Id,
    AnnotationKind Kind,
    string? Label,
    string Color,
    string GeometryJson,
    DateTimeOffset CreatedAt);
