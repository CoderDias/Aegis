namespace Aegis.Application.Dtos.Intel;

public record GeoMarkerDto(
    string Id,
    string Kind,
    string Title,
    string? Subtitle,
    double Lat,
    double Lng,
    double Weight = 1,
    DateTimeOffset? Timestamp = null,
    string? Detail = null,
    string? Source = null,
    string? Severity = null,
    string? Region = null,
    DateTimeOffset? ValidUntil = null,
    string? EventType = null,
    string? Instructions = null,
    string? Risks = null);
