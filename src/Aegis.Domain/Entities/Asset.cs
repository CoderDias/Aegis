using Aegis.Domain.Enums;
using Aegis.Domain.Exceptions;
using Aegis.Domain.ValueObjects;

namespace Aegis.Domain.Entities;

public class Asset
{
    public const int MaxMetadataJsonLength = 16 * 1024;

    public Guid Id { get; private set; }
    public Guid InvestigationId { get; private set; }
    public AssetType Type { get; private set; }
    public string DisplayName { get; private set; } = string.Empty;
    public DataSourceType Source { get; private set; }
    public string? ExternalKey { get; private set; }
    public Coordinate? Location { get; private set; }
    public string MetadataJson { get; private set; } = "{}";
    public DateTimeOffset CreatedAt { get; private set; }
    public string? Notes { get; private set; }

    private Asset()
    {
    }

    public static Asset Create(
        Guid id,
        Guid investigationId,
        AssetType type,
        string displayName,
        DataSourceType source,
        string? externalKey,
        Coordinate? location,
        string metadataJson,
        DateTimeOffset createdAt,
        string? notes = null)
    {
        if (id == Guid.Empty)
        {
            throw new DomainException("Asset id cannot be empty.");
        }

        if (investigationId == Guid.Empty)
        {
            throw new DomainException("Investigation id cannot be empty.");
        }

        var normalizedName = ValidateDisplayName(displayName);
        var normalizedMetadata = ValidateMetadataJson(metadataJson);

        return new Asset
        {
            Id = id,
            InvestigationId = investigationId,
            Type = type,
            DisplayName = normalizedName,
            Source = source,
            ExternalKey = NormalizeExternalKey(externalKey),
            Location = location,
            MetadataJson = normalizedMetadata,
            CreatedAt = createdAt,
            Notes = NormalizeOptionalText(notes)
        };
    }

    internal static string? ComputeNaturalKey(Asset asset)
    {
        ArgumentNullException.ThrowIfNull(asset);

        return asset.Type switch
        {
            AssetType.Aircraft => asset.ExternalKey?.ToLowerInvariant(),
            AssetType.Building => asset.ExternalKey,
            AssetType.Coordinate when asset.Location is not null =>
                $"{Math.Round(asset.Location.Latitude, 6):F6}|{Math.Round(asset.Location.Longitude, 6):F6}|{GetCoordinateLabel(asset)}",
            _ => asset.ExternalKey
        };
    }

    private static string GetCoordinateLabel(Asset asset)
    {
        if (!string.IsNullOrWhiteSpace(asset.DisplayName))
        {
            return asset.DisplayName.Trim();
        }

        return string.Empty;
    }

    private static string ValidateDisplayName(string displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName))
        {
            throw new DomainException("Asset display name is required.");
        }

        var trimmed = displayName.Trim();
        if (trimmed.Length > 200)
        {
            throw new DomainException("Asset display name cannot exceed 200 characters.");
        }

        return trimmed;
    }

    private static string ValidateMetadataJson(string metadataJson)
    {
        var json = string.IsNullOrWhiteSpace(metadataJson) ? "{}" : metadataJson.Trim();
        if (json.Length > MaxMetadataJsonLength)
        {
            throw new DomainException($"Asset metadata JSON cannot exceed {MaxMetadataJsonLength} characters.");
        }

        return json;
    }

    public void UpdateNotes(string? notes)
    {
        Notes = NormalizeOptionalText(notes);
    }

    private static string? NormalizeExternalKey(string? externalKey) =>
        string.IsNullOrWhiteSpace(externalKey) ? null : externalKey.Trim();

    private static string? NormalizeOptionalText(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
