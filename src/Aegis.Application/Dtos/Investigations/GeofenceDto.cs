namespace Aegis.Application.Dtos.Investigations;

public record GeofenceDto(
    Guid Id,
    string Name,
    string GeometryJson,
    bool IsEnabled,
    DateTimeOffset CreatedAt);
