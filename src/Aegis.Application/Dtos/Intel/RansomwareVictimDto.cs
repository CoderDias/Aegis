namespace Aegis.Application.Dtos.Intel;

public record RansomwareVictimDto(
    string Victim,
    string Group,
    string? Country,
    string? Domain,
    string? Activity,
    string Url,
    DateTimeOffset DiscoveredAt,
    double? Lat,
    double? Lng);
