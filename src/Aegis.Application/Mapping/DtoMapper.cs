using Aegis.Application.Dtos.Flights;
using Aegis.Application.Dtos.Geo;
using Aegis.Application.Dtos.Investigations;
using Aegis.Application.Dtos.Map;
using Aegis.Domain.Entities;
using Aegis.Domain.ValueObjects;

namespace Aegis.Application.Mapping;

public static class DtoMapper
{
    public static CoordinateDto ToDto(this Coordinate coordinate) =>
        new(coordinate.Latitude, coordinate.Longitude);

    public static Coordinate ToDomain(this CoordinateDto dto) =>
        Coordinate.Create(dto.Lat, dto.Lng);

    public static BoundingBoxDto ToDto(this BoundingBox box) =>
        new(box.South, box.West, box.North, box.East);

    public static BoundingBox ToDomain(this BoundingBoxDto dto) =>
        BoundingBox.Create(dto.South, dto.West, dto.North, dto.East);

    public static InvestigationSummaryDto ToSummaryDto(this Investigation investigation) =>
        new(
            investigation.Id,
            investigation.Title,
            investigation.Status,
            investigation.UpdatedAt,
            investigation.Assets.Count);

    public static InvestigationDetailDto ToDetailDto(this Investigation investigation) =>
        new(
            investigation.Id,
            investigation.Title,
            investigation.Description,
            investigation.Status,
            investigation.CreatedAt,
            investigation.UpdatedAt,
            investigation.ClosedAt,
            investigation.Assets.Select(a => a.ToDto()).ToList(),
            investigation.Annotations.Select(a => a.ToDto()).ToList(),
            investigation.Timeline.Select(e => e.ToDto()).ToList(),
            investigation.Geofences.Select(g => g.ToDto()).ToList());

    public static AssetDto ToDto(this Asset asset) =>
        new(
            asset.Id,
            asset.InvestigationId,
            asset.Type,
            asset.DisplayName,
            asset.Source,
            asset.ExternalKey,
            asset.Location?.ToDto(),
            asset.MetadataJson,
            asset.CreatedAt,
            asset.Notes);

    public static AnnotationDto ToDto(this Annotation annotation) =>
        new(
            annotation.Id,
            annotation.Kind,
            annotation.Label,
            annotation.Color,
            annotation.GeometryJson,
            annotation.CreatedAt);

    public static TimelineEventDto ToDto(this TimelineEvent timelineEvent) =>
        new(
            timelineEvent.Id,
            timelineEvent.OccurredAt,
            timelineEvent.Type,
            timelineEvent.Message,
            timelineEvent.PayloadJson,
            timelineEvent.IsRead);

    public static GeofenceDto ToDto(this Geofence geofence) =>
        new(
            geofence.Id,
            geofence.Name,
            geofence.GeometryJson,
            geofence.IsEnabled,
            geofence.CreatedAt);

    public static AircraftMarkerDto ToMarkerDto(this FlightTrackPoint point) =>
        new(
            point.Icao24,
            point.Callsign,
            point.Latitude,
            point.Longitude,
            point.BaroAltitude,
            point.Velocity,
            point.Heading,
            point.OriginCountry,
            point.OnGround,
            point.Time);

}
