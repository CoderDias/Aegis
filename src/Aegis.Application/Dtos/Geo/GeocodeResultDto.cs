using Aegis.Application.Dtos.Geo;

namespace Aegis.Application.Dtos.Geo;

public record GeocodeResultDto(
    string DisplayName,
    CoordinateDto Coordinate,
    string? Type,
    long? OsmId,
    IReadOnlyDictionary<string, string>? AddressParts);
