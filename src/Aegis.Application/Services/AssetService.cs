using System.Text.Json;
using Aegis.Application.Abstractions;
using Aegis.Application.Dtos.Flights;
using Aegis.Application.Dtos.Geo;
using Aegis.Application.Dtos.Intel;
using Aegis.Application.Dtos.Investigations;
using Aegis.Application.Dtos.Map;
using Aegis.Application.Mapping;
using Aegis.Domain.Entities;
using Aegis.Domain.Enums;
using Aegis.Domain.Exceptions;
using Aegis.Domain.ValueObjects;

namespace Aegis.Application.Services;

public sealed class AssetService(
    IInvestigationStore store,
    IFlightTrackingService flightTracking,
    IClock clock)
{
    public async Task<AssetDto> AddFromAircraftAsync(
        Guid investigationId,
        string icao24,
        CancellationToken cancellationToken = default)
    {
        var marker = await flightTracking.GetByIcaoAsync(icao24, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Aircraft {icao24} not found in current flight cache.");

        var investigation = await GetMutableInvestigationAsync(investigationId, cancellationToken).ConfigureAwait(false);
        var metadata = JsonSerializer.Serialize(new
        {
            icao24 = marker.Icao24,
            callsign = marker.Callsign,
            originCountry = marker.OriginCountry,
            lastHeading = marker.Heading
        });

        var location = Coordinate.Create(marker.Lat, marker.Lng);
        var displayName = string.IsNullOrWhiteSpace(marker.Callsign) ? marker.Icao24 : marker.Callsign.Trim();
        var asset = Asset.Create(
            Guid.NewGuid(),
            investigationId,
            AssetType.Aircraft,
            displayName,
            DataSourceType.OpenSky,
            marker.Icao24,
            location,
            metadata,
            clock.UtcNow);

        investigation.AddAsset(asset, clock.UtcNow);
        await store.SaveAsync(investigation, cancellationToken).ConfigureAwait(false);
        return asset.ToDto();
    }

    public async Task<AssetDto> AddFromMapFeatureAsync(
        Guid investigationId,
        MapFeatureDto feature,
        CancellationToken cancellationToken = default)
    {
        var investigation = await GetMutableInvestigationAsync(investigationId, cancellationToken).ConfigureAwait(false);
        var metadata = JsonSerializer.Serialize(new
        {
            osmType = feature.OsmType,
            osmId = feature.OsmId,
            category = feature.Category,
            tags = feature.Tags
        });

        var location = feature.Centroid.ToDomain();
        var displayName = string.IsNullOrWhiteSpace(feature.Name) ? $"{feature.OsmType}/{feature.OsmId}" : feature.Name;
        var externalKey = $"{feature.OsmType}:{feature.OsmId}";
        var asset = Asset.Create(
            Guid.NewGuid(),
            investigationId,
            AssetType.Building,
            displayName,
            DataSourceType.Overpass,
            externalKey,
            location,
            metadata,
            clock.UtcNow);

        investigation.AddAsset(asset, clock.UtcNow);
        await store.SaveAsync(investigation, cancellationToken).ConfigureAwait(false);
        return asset.ToDto();
    }

    public async Task<AssetDto> AddCoordinateAsync(
        Guid investigationId,
        CoordinateDto coordinate,
        string label,
        CancellationToken cancellationToken = default)
    {
        var investigation = await GetMutableInvestigationAsync(investigationId, cancellationToken).ConfigureAwait(false);
        var location = coordinate.ToDomain();
        var metadata = JsonSerializer.Serialize(new { label });
        var asset = Asset.Create(
            Guid.NewGuid(),
            investigationId,
            AssetType.Coordinate,
            label,
            DataSourceType.Manual,
            externalKey: null,
            location,
            metadata,
            clock.UtcNow);

        investigation.AddAsset(asset, clock.UtcNow);
        await store.SaveAsync(investigation, cancellationToken).ConfigureAwait(false);
        return asset.ToDto();
    }

    public async Task<AssetDto> AddFromShodanHostAsync(
        Guid investigationId,
        ShodanHostDto host,
        CancellationToken cancellationToken = default)
    {
        var investigation = await GetMutableInvestigationAsync(investigationId, cancellationToken).ConfigureAwait(false);
        var metadata = JsonSerializer.Serialize(new
        {
            ip = host.Ip,
            port = host.Port,
            transport = host.Transport,
            org = host.Org,
            product = host.Product,
            hostnames = host.Hostnames,
            city = host.City,
            country = host.Country
        });

        var location = Coordinate.Create(host.Lat, host.Lng);
        var displayName = $"{host.Ip}:{host.Port}";
        var asset = Asset.Create(
            Guid.NewGuid(),
            investigationId,
            AssetType.Host,
            displayName,
            DataSourceType.HostDiscovery,
            host.Ip,
            location,
            metadata,
            clock.UtcNow);

        investigation.AddAsset(asset, clock.UtcNow);
        await store.SaveAsync(investigation, cancellationToken).ConfigureAwait(false);
        return asset.ToDto();
    }

    public async Task<AssetDto> AddManualAsync(
        Guid investigationId,
        AssetType type,
        string displayName,
        string? notes,
        string metadataJson = "{}",
        CoordinateDto? location = null,
        CancellationToken cancellationToken = default)
    {
        var investigation = await GetMutableInvestigationAsync(investigationId, cancellationToken).ConfigureAwait(false);
        var asset = Asset.Create(
            Guid.NewGuid(),
            investigationId,
            type,
            displayName,
            DataSourceType.Manual,
            externalKey: null,
            location?.ToDomain(),
            metadataJson,
            clock.UtcNow,
            notes);

        investigation.AddAsset(asset, clock.UtcNow);
        await store.SaveAsync(investigation, cancellationToken).ConfigureAwait(false);
        return asset.ToDto();
    }

    public async Task<AssetDto> UpdateNotesAsync(
        Guid investigationId,
        Guid assetId,
        string? notes,
        CancellationToken cancellationToken = default)
    {
        var investigation = await GetMutableInvestigationAsync(investigationId, cancellationToken).ConfigureAwait(false);
        var asset = investigation.Assets.FirstOrDefault(a => a.Id == assetId)
            ?? throw new InvalidOperationException($"Asset {assetId} not found.");

        asset.UpdateNotes(notes);
        await store.SaveAsync(investigation, cancellationToken).ConfigureAwait(false);
        return asset.ToDto();
    }

    public async Task RemoveAsync(
        Guid investigationId,
        Guid assetId,
        CancellationToken cancellationToken = default)
    {
        var investigation = await GetMutableInvestigationAsync(investigationId, cancellationToken).ConfigureAwait(false);
        investigation.RemoveAsset(assetId, clock.UtcNow);
        await store.SaveAsync(investigation, cancellationToken).ConfigureAwait(false);
    }

    public async Task<AssetDto> AddFromMapSelectionAsync(
        Guid investigationId,
        string kind,
        string id,
        string title,
        CoordinateDto? location,
        MapFeatureDto? feature,
        AircraftMarkerDto? aircraft,
        ShodanHostDto? host,
        NewsItemDto? news,
        RansomwareVictimDto? ransomware,
        GeoMarkerDto? geoMarker,
        string metadataJson = "{}",
        CancellationToken cancellationToken = default)
    {
        return kind switch
        {
            "aircraft" => await AddFromAircraftAsync(
                investigationId,
                aircraft?.Icao24 ?? id,
                cancellationToken).ConfigureAwait(false),
            "shodan" when host is not null => await AddFromShodanHostAsync(
                investigationId,
                host,
                cancellationToken).ConfigureAwait(false),
            "public_camera" or "erb" or "port" or "feature" or "radio_tower" or "repeater" or "building"
                or "public_building" or "road" or "poi" when feature is not null => await AddFromMapFeatureAsync(
                    investigationId,
                    feature,
                    cancellationToken).ConfigureAwait(false),
            "geocode" or "coordinate" when location is not null => await AddCoordinateAsync(
                investigationId,
                location,
                title,
                cancellationToken).ConfigureAwait(false),
            "news" when news is not null => await AddManualAsync(
                investigationId,
                AssetType.Other,
                news.Title,
                notes: null,
                metadataJson: JsonSerializer.Serialize(new
                {
                    feedId = news.FeedId,
                    feedTitle = news.FeedTitle,
                    link = news.Link,
                    publishedAt = news.PublishedAt,
                    summary = news.Summary
                }),
                location: news.Lat is not null && news.Lng is not null
                    ? new CoordinateDto(news.Lat.Value, news.Lng.Value)
                    : location,
                cancellationToken: cancellationToken).ConfigureAwait(false),
            "ransomware" when ransomware is not null => await AddManualAsync(
                investigationId,
                AssetType.Other,
                ransomware.Victim,
                notes: null,
                metadataJson: JsonSerializer.Serialize(new
                {
                    group = ransomware.Group,
                    country = ransomware.Country,
                    domain = ransomware.Domain,
                    activity = ransomware.Activity,
                    url = ransomware.Url,
                    discoveredAt = ransomware.DiscoveredAt
                }),
                location: ransomware.Lat is not null && ransomware.Lng is not null
                    ? new CoordinateDto(ransomware.Lat.Value, ransomware.Lng.Value)
                    : location,
                cancellationToken: cancellationToken).ConfigureAwait(false),
            "seismic" or "ships" or "alerts" when geoMarker is not null => await AddManualAsync(
                investigationId,
                AssetType.Other,
                geoMarker.Title,
                notes: null,
                metadataJson: JsonSerializer.Serialize(new
                {
                    kind,
                    geoMarker.Id,
                    geoMarker.Kind,
                    geoMarker.Subtitle,
                    geoMarker.Detail,
                    geoMarker.Source,
                    geoMarker.Severity,
                    geoMarker.Region,
                    geoMarker.EventType,
                    geoMarker.Timestamp,
                    geoMarker.ValidUntil
                }),
                location: new CoordinateDto(geoMarker.Lat, geoMarker.Lng),
                cancellationToken: cancellationToken).ConfigureAwait(false),
            _ => await AddManualAsync(
                investigationId,
                AssetType.Other,
                title,
                notes: null,
                metadataJson: string.IsNullOrWhiteSpace(metadataJson) ? "{}" : metadataJson,
                location,
                cancellationToken: cancellationToken).ConfigureAwait(false)
        };
    }

    private async Task<Investigation> GetMutableInvestigationAsync(Guid id, CancellationToken cancellationToken)
    {
        var investigation = await store.GetAsync(id, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Investigation {id} not found.");

        if (investigation.Status == InvestigationStatus.Archived)
        {
            throw new DomainException("Archived investigations are read-only.");
        }

        return investigation;
    }
}
