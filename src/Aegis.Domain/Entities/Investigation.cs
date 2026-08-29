using Aegis.Domain.Enums;
using Aegis.Domain.Exceptions;

namespace Aegis.Domain.Entities;

public class Investigation
{
    public const int MaxAssets = 500;
    public const int MaxAnnotations = 100;
    public const int MaxGeofences = 20;
    public const int MaxTitleLength = 120;
    public const int MaxDescriptionLength = 4000;

    private readonly List<Asset> _assets = [];
    private readonly List<Annotation> _annotations = [];
    private readonly List<TimelineEvent> _timeline = [];
    private readonly List<Geofence> _geofences = [];

    public Guid Id { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public InvestigationStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public DateTimeOffset? ClosedAt { get; private set; }

    public IReadOnlyList<Asset> Assets => _assets;
    public IReadOnlyList<Annotation> Annotations => _annotations;
    public IReadOnlyList<TimelineEvent> Timeline => _timeline;
    public IReadOnlyList<Geofence> Geofences => _geofences;

    private Investigation()
    {
    }

    public static Investigation Create(string title, string? description, DateTimeOffset utcNow)
    {
        var investigation = new Investigation
        {
            Id = Guid.NewGuid(),
            Title = ValidateTitle(title),
            Description = NormalizeDescription(description),
            Status = InvestigationStatus.Active,
            CreatedAt = utcNow,
            UpdatedAt = utcNow,
            ClosedAt = null
        };

        investigation.RecordTimeline(
            TimelineEventType.InvestigationCreated,
            "Investigation created.",
            null,
            utcNow);

        return investigation;
    }

    public void AddAsset(Asset asset, DateTimeOffset utcNow)
    {
        ArgumentNullException.ThrowIfNull(asset);
        EnsureMutable();

        if (asset.InvestigationId != Id)
        {
            throw new DomainException("Asset belongs to a different investigation.");
        }

        if (_assets.Count >= MaxAssets)
        {
            throw new DomainException($"An investigation cannot have more than {MaxAssets} assets.");
        }

        var naturalKey = Asset.ComputeNaturalKey(asset);
        if (naturalKey is not null &&
            _assets.Any(existing =>
                existing.Type == asset.Type &&
                string.Equals(Asset.ComputeNaturalKey(existing), naturalKey, StringComparison.Ordinal)))
        {
            throw new DomainException("An asset with the same natural key already exists in this investigation.");
        }

        _assets.Add(asset);
        Touch(utcNow);
        RecordTimeline(
            TimelineEventType.AssetAdded,
            $"Asset '{asset.DisplayName}' added.",
            null,
            utcNow);
    }

    public void RemoveAsset(Guid assetId, DateTimeOffset utcNow)
    {
        EnsureMutable();

        var asset = _assets.FirstOrDefault(a => a.Id == assetId);
        if (asset is null)
        {
            throw new DomainException("Asset not found in this investigation.");
        }

        _assets.Remove(asset);
        Touch(utcNow);
        RecordTimeline(
            TimelineEventType.AssetRemoved,
            $"Asset '{asset.DisplayName}' removed.",
            null,
            utcNow);
    }

    public void AddAnnotation(Annotation annotation, DateTimeOffset utcNow)
    {
        ArgumentNullException.ThrowIfNull(annotation);
        EnsureMutable();

        if (annotation.InvestigationId != Id)
        {
            throw new DomainException("Annotation belongs to a different investigation.");
        }

        if (_annotations.Count >= MaxAnnotations)
        {
            throw new DomainException($"An investigation cannot have more than {MaxAnnotations} annotations.");
        }

        _annotations.Add(annotation);
        Touch(utcNow);
        RecordTimeline(
            TimelineEventType.AnnotationAdded,
            $"Annotation '{annotation.Label ?? annotation.Kind.ToString()}' added.",
            null,
            utcNow);
    }

    public void AddGeofence(Geofence geofence, DateTimeOffset utcNow)
    {
        ArgumentNullException.ThrowIfNull(geofence);
        EnsureMutable();

        if (geofence.InvestigationId != Id)
        {
            throw new DomainException("Geofence belongs to a different investigation.");
        }

        if (_geofences.Count >= MaxGeofences)
        {
            throw new DomainException($"An investigation cannot have more than {MaxGeofences} geofences.");
        }

        _geofences.Add(geofence);
        Touch(utcNow);
        RecordTimeline(
            TimelineEventType.Note,
            $"Geofence '{geofence.Name}' added.",
            null,
            utcNow);
    }

    public void RemoveGeofence(Guid geofenceId, DateTimeOffset utcNow)
    {
        EnsureMutable();

        var geofence = _geofences.FirstOrDefault(g => g.Id == geofenceId)
            ?? throw new DomainException("Geofence not found in this investigation.");

        _geofences.Remove(geofence);
        Touch(utcNow);
        RecordTimeline(
            TimelineEventType.Note,
            $"Geofence '{geofence.Name}' removed.",
            null,
            utcNow);
    }

    public void ToggleGeofence(Guid geofenceId, DateTimeOffset utcNow)
    {
        EnsureMutable();

        var geofence = _geofences.FirstOrDefault(g => g.Id == geofenceId)
            ?? throw new DomainException("Geofence not found in this investigation.");

        if (geofence.IsEnabled)
            geofence.Disable();
        else
            geofence.Enable();

        Touch(utcNow);
    }

    public void AddNote(string message, DateTimeOffset utcNow)
    {
        EnsureMutable();

        if (string.IsNullOrWhiteSpace(message))
        {
            throw new DomainException("Note message is required.");
        }

        var trimmed = message.Trim();
        if (trimmed.Length > 2000)
        {
            throw new DomainException("Note message cannot exceed 2000 characters.");
        }

        Touch(utcNow);
        RecordTimeline(TimelineEventType.Note, trimmed, null, utcNow);
    }

    public void ChangeStatus(InvestigationStatus status, DateTimeOffset utcNow)
    {
        EnsureMutable();

        if (Status == status)
        {
            return;
        }

        Status = status;
        ClosedAt = status is InvestigationStatus.Completed or InvestigationStatus.Archived
            ? utcNow
            : null;

        Touch(utcNow);
        RecordTimeline(
            TimelineEventType.Note,
            $"Investigation status changed to {status}.",
            null,
            utcNow);
    }

    public void Rename(string title, DateTimeOffset utcNow)
    {
        EnsureMutable();

        var normalizedTitle = ValidateTitle(title);
        if (Title == normalizedTitle)
        {
            return;
        }

        Title = normalizedTitle;
        Touch(utcNow);
        RecordTimeline(
            TimelineEventType.Note,
            $"Investigation renamed to '{normalizedTitle}'.",
            null,
            utcNow);
    }

    public void UpdateDetails(string title, string? description, DateTimeOffset utcNow)
    {
        EnsureMutable();

        Title = ValidateTitle(title);
        Description = NormalizeDescription(description);
        Touch(utcNow);
        RecordTimeline(
            TimelineEventType.Note,
            "Investigation details updated.",
            null,
            utcNow);
    }

    public void UpdateAnnotationGeometry(
        Guid annotationId,
        string? label,
        string color,
        string geometryJson,
        DateTimeOffset utcNow)
    {
        EnsureMutable();

        var annotation = _annotations.FirstOrDefault(a => a.Id == annotationId)
            ?? throw new DomainException("Annotation not found in this investigation.");

        annotation.Update(label, color, geometryJson);
        Touch(utcNow);
        RecordTimeline(
            TimelineEventType.Note,
            $"Annotation '{annotation.Label ?? annotation.Kind.ToString()}' updated.",
            null,
            utcNow);
    }

    public void RemoveAnnotation(Guid annotationId, DateTimeOffset utcNow)
    {
        EnsureMutable();

        var annotation = _annotations.FirstOrDefault(a => a.Id == annotationId)
            ?? throw new DomainException("Annotation not found in this investigation.");

        _annotations.Remove(annotation);
        Touch(utcNow);
        RecordTimeline(
            TimelineEventType.Note,
            $"Annotation '{annotation.Label ?? annotation.Kind.ToString()}' removed.",
            null,
            utcNow);
    }

    public void RecordOsintSourceAccess(
        string fonteName,
        string url,
        DateTimeOffset utcNow)
    {
        if (string.IsNullOrWhiteSpace(fonteName))
        {
            throw new DomainException("Source name is required.");
        }

        if (string.IsNullOrWhiteSpace(url))
        {
            throw new DomainException("Source URL is required.");
        }

        EnsureMutable();
        Touch(utcNow);

        var payload = System.Text.Json.JsonSerializer.Serialize(new
        {
            url = url.Trim(),
            fonte = fonteName.Trim()
        });

        RecordTimeline(
            TimelineEventType.OsintSourceAccess,
            $"Fonte OSINT aberta: {fonteName.Trim()}",
            payload,
            utcNow);
    }

    public void RecordGeofenceAlert(
        Geofence geofence,
        string icao24,
        string? callsign,
        string message,
        DateTimeOffset utcNow)
    {
        ArgumentNullException.ThrowIfNull(geofence);

        EnsureMutable();
        Touch(utcNow);

        var payload = System.Text.Json.JsonSerializer.Serialize(new
        {
            geofenceId = geofence.Id,
            geofenceName = geofence.Name,
            icao24,
            callsign
        });

        RecordTimeline(TimelineEventType.GeofenceAlert, message, payload, utcNow);
    }

    public void MarkAlertRead(Guid eventId, DateTimeOffset utcNow)
    {
        var timelineEvent = _timeline.FirstOrDefault(e =>
            e.Id == eventId && e.Type == TimelineEventType.GeofenceAlert);
        if (timelineEvent is null || timelineEvent.IsRead)
        {
            return;
        }

        timelineEvent.MarkAsRead();
        Touch(utcNow);
    }

    public void MarkAllAlertsRead(DateTimeOffset utcNow)
    {
        var unread = _timeline
            .Where(e => e.Type == TimelineEventType.GeofenceAlert && !e.IsRead)
            .ToList();
        if (unread.Count == 0)
        {
            return;
        }

        foreach (var timelineEvent in unread)
        {
            timelineEvent.MarkAsRead();
        }

        Touch(utcNow);
    }

    private void EnsureMutable()
    {
        if (Status == InvestigationStatus.Archived)
        {
            throw new DomainException("Archived investigations are read-only.");
        }
    }

    private void Touch(DateTimeOffset utcNow) => UpdatedAt = utcNow;

    private void RecordTimeline(
        TimelineEventType type,
        string message,
        string? payloadJson,
        DateTimeOffset occurredAt)
    {
        var timelineEvent = TimelineEvent.Create(
            Guid.NewGuid(),
            Id,
            occurredAt,
            type,
            message,
            payloadJson);

        _timeline.Add(timelineEvent);
    }

    private static string ValidateTitle(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new DomainException("Investigation title is required.");
        }

        var trimmed = title.Trim();
        if (trimmed.Length > MaxTitleLength)
        {
            throw new DomainException($"Investigation title cannot exceed {MaxTitleLength} characters.");
        }

        return trimmed;
    }

    private static string? NormalizeDescription(string? description)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            return null;
        }

        var trimmed = description.Trim();
        if (trimmed.Length > MaxDescriptionLength)
        {
            throw new DomainException($"Investigation description cannot exceed {MaxDescriptionLength} characters.");
        }

        return trimmed;
    }
}
