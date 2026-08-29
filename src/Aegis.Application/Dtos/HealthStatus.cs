namespace Aegis.Application.Dtos;

public record HealthStatus(
    bool IsHealthy,
    string? Message,
    TimeSpan? Latency,
    DateTimeOffset CheckedAt);
