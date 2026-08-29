using Aegis.Domain.Enums;
using Aegis.Domain.Exceptions;

namespace Aegis.Domain.Entities;

public class TimelineEvent
{
    public Guid Id { get; private set; }
    public Guid InvestigationId { get; private set; }
    public DateTimeOffset OccurredAt { get; private set; }
    public TimelineEventType Type { get; private set; }
    public string Message { get; private set; } = string.Empty;
    public string? PayloadJson { get; private set; }
    public bool IsRead { get; private set; }

    private TimelineEvent()
    {
    }

    public static TimelineEvent Create(
        Guid id,
        Guid investigationId,
        DateTimeOffset occurredAt,
        TimelineEventType type,
        string message,
        string? payloadJson = null)
    {
        if (id == Guid.Empty)
        {
            throw new DomainException("Timeline event id cannot be empty.");
        }

        if (investigationId == Guid.Empty)
        {
            throw new DomainException("Investigation id cannot be empty.");
        }

        if (string.IsNullOrWhiteSpace(message))
        {
            throw new DomainException("Timeline event message is required.");
        }

        var trimmedMessage = message.Trim();
        if (trimmedMessage.Length > 2000)
        {
            throw new DomainException("Timeline event message cannot exceed 2000 characters.");
        }

        return new TimelineEvent
        {
            Id = id,
            InvestigationId = investigationId,
            OccurredAt = occurredAt,
            Type = type,
            Message = trimmedMessage,
            PayloadJson = string.IsNullOrWhiteSpace(payloadJson) ? null : payloadJson.Trim(),
            IsRead = false
        };
    }

    public void MarkAsRead() => IsRead = true;
}
