using Aegis.Domain.Enums;

namespace Aegis.Application.Dtos.Investigations;

public record InvestigationSummaryDto(
    Guid Id,
    string Title,
    InvestigationStatus Status,
    DateTimeOffset UpdatedAt,
    int AssetCount);
