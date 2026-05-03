namespace SteelProgress.App.Services;

// Servicio global para mostrar notificaciones tipo toast en la UI
public static class NotificationService
{
    public static event Action<string, NotificationType>? OnNotificationRequested;

    public static void Info(string message)
    {
        OnNotificationRequested?.Invoke(message, NotificationType.Info);
    }

    public static void Success(string message)
    {
        OnNotificationRequested?.Invoke(message, NotificationType.Success);
    }

    public static void Error(string message)
    {
        OnNotificationRequested?.Invoke(message, NotificationType.Error);
    }
}

public enum NotificationType
{
    Info,
    Success,
    Error
}