namespace Aegis.Web.Services;

public enum NotificationLevel
{
    Info,
    Success,
    Warning,
    Danger
}

public sealed record UiNotification(
    Guid Id,
    string Message,
    NotificationLevel Level,
    DateTimeOffset CreatedAt,
    TimeSpan Duration);

public sealed class UiNotificationService
{
    private readonly List<UiNotification> _notifications = [];

    public IReadOnlyList<UiNotification> Notifications => _notifications;

    public event Action? Changed;

    public void Show(string message, NotificationLevel level = NotificationLevel.Info, TimeSpan? duration = null)
    {
        var notification = new UiNotification(
            Guid.NewGuid(),
            message,
            level,
            DateTimeOffset.UtcNow,
            duration ?? TimeSpan.FromSeconds(8));

        _notifications.Add(notification);
        Changed?.Invoke();
    }

    public void Dismiss(Guid id)
    {
        _notifications.RemoveAll(n => n.Id == id);
        Changed?.Invoke();
    }

    public void Clear()
    {
        _notifications.Clear();
        Changed?.Invoke();
    }
}
