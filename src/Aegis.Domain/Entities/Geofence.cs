using Aegis.Domain.Exceptions;

namespace Aegis.Domain.Entities;

public class Geofence
{
    public Guid Id { get; private set; }
    public Guid InvestigationId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string GeometryJson { get; private set; } = string.Empty;
    public bool IsEnabled { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    private Geofence()
    {
    }

    public void Enable() => IsEnabled = true;

    public void Disable() => IsEnabled = false;

    public void Rename(string newName)
    {
        if (string.IsNullOrWhiteSpace(newName))
            throw new DomainException("Geofence name is required.");

        var trimmed = newName.Trim();
        if (trimmed.Length > 120)
            throw new DomainException("Geofence name cannot exceed 120 characters.");

        Name = trimmed;
    }

    public static Geofence Create(
        Guid id,
        Guid investigationId,
        string name,
        string geometryJson,
        DateTimeOffset createdAt,
        bool isEnabled = true)
    {
        if (id == Guid.Empty)
        {
            throw new DomainException("Geofence id cannot be empty.");
        }

        if (investigationId == Guid.Empty)
        {
            throw new DomainException("Investigation id cannot be empty.");
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("Geofence name is required.");
        }

        if (string.IsNullOrWhiteSpace(geometryJson))
        {
            throw new DomainException("Geofence geometry JSON is required.");
        }

        var trimmedName = name.Trim();
        if (trimmedName.Length > 120)
        {
            throw new DomainException("Geofence name cannot exceed 120 characters.");
        }

        return new Geofence
        {
            Id = id,
            InvestigationId = investigationId,
            Name = trimmedName,
            GeometryJson = geometryJson.Trim(),
            IsEnabled = isEnabled,
            CreatedAt = createdAt
        };
    }
}
