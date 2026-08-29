using Aegis.Application.Dtos.Geo;
using Aegis.Domain.Enums;

namespace Aegis.Application.Dtos.Investigations;

public record AssetDto(
    Guid Id,
    Guid InvestigationId,
    AssetType Type,
    string DisplayName,
    DataSourceType Source,
    string? ExternalKey,
    CoordinateDto? Location,
    string MetadataJson,
    DateTimeOffset CreatedAt,
    string? Notes);
