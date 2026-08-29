using Aegis.Domain.Enums;
using Aegis.Domain.Exceptions;

namespace Aegis.Domain.Entities;

public class Annotation
{
    public const string DefaultColor = "#3ec6e0";

    public Guid Id { get; private set; }
    public Guid InvestigationId { get; private set; }
    public AnnotationKind Kind { get; private set; }
    public string? Label { get; private set; }
    public string Color { get; private set; } = DefaultColor;
    public string GeometryJson { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; private set; }

    private Annotation()
    {
    }

    public static Annotation Create(
        Guid id,
        Guid investigationId,
        AnnotationKind kind,
        string geometryJson,
        DateTimeOffset createdAt,
        string? label = null,
        string? color = null)
    {
        if (id == Guid.Empty)
        {
            throw new DomainException("Annotation id cannot be empty.");
        }

        if (investigationId == Guid.Empty)
        {
            throw new DomainException("Investigation id cannot be empty.");
        }

        if (string.IsNullOrWhiteSpace(geometryJson))
        {
            throw new DomainException("Annotation geometry JSON is required.");
        }

        return new Annotation
        {
            Id = id,
            InvestigationId = investigationId,
            Kind = kind,
            Label = string.IsNullOrWhiteSpace(label) ? null : label.Trim(),
            Color = ValidateColor(color),
            GeometryJson = geometryJson.Trim(),
            CreatedAt = createdAt
        };
    }

    private static string ValidateColor(string? color)
    {
        if (string.IsNullOrWhiteSpace(color))
        {
            return DefaultColor;
        }

        var trimmed = color.Trim();
        if (trimmed.Length != 7 || trimmed[0] != '#')
        {
            throw new DomainException("Annotation color must be a hex value in the form #RRGGBB.");
        }

        for (var i = 1; i < trimmed.Length; i++)
        {
            if (!Uri.IsHexDigit(trimmed[i]))
            {
                throw new DomainException("Annotation color must be a hex value in the form #RRGGBB.");
            }
        }

        return trimmed.ToLowerInvariant();
    }

    public void Update(string? label, string color, string geometryJson)
    {
        if (string.IsNullOrWhiteSpace(geometryJson))
        {
            throw new DomainException("Annotation geometry JSON is required.");
        }

        Label = string.IsNullOrWhiteSpace(label) ? null : label.Trim();
        Color = ValidateColor(color);
        GeometryJson = geometryJson.Trim();
    }
}
