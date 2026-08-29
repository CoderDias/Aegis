using Aegis.Domain.Enums;

namespace Aegis.Application.Dtos.Investigations;

public record TimelineEventDto(
    Guid Id,
    DateTimeOffset OccurredAt,
    TimelineEventType Type,
    string Message,
    string? PayloadJson,
    bool IsRead);
