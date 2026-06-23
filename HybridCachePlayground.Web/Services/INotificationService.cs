using HybridCachePlayground.Web.Models;

namespace HybridCachePlayground.Web.Services;

public interface INotificationService
{
    /// <summary>
    /// Create and store a notification, then call the delivery stub.
    /// Call this from anywhere in the application to trigger a notification.
    /// </summary>
    Task NotifyAsync(string title, string message, NotificationLevel level = NotificationLevel.Info);

    IReadOnlyList<NotificationEntry> GetRecent(int count = 50);
    int  GetUnreadCount();
    void MarkAllRead();
}
