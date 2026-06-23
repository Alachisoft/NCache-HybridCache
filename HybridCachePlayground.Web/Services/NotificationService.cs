using System.Collections.Concurrent;
using HybridCachePlayground.Web.Models;

namespace HybridCachePlayground.Web.Services;

public sealed class NotificationService : INotificationService
{
    private readonly ILogger<NotificationService> _logger;
    private readonly ConcurrentQueue<NotificationEntry> _store = new();
    private const int MaxEntries = 100;

    public NotificationService(ILogger<NotificationService> logger)
    {
        _logger = logger;
    }

    public async Task NotifyAsync(
        string title, string message, NotificationLevel level = NotificationLevel.Info)
    {
        var entry = new NotificationEntry
        {
            Title   = title,
            Message = message,
            Level   = level
        };

        _store.Enqueue(entry);

        // Trim oldest entries when cap is exceeded
        while (_store.Count > MaxEntries)
            _store.TryDequeue(out _);

        _logger.LogInformation(
            "Notification [{Level}] {Title}: {Message}", level, title, message);

        await DeliverAsync(entry);
    }

    /// <summary>
    /// ── USER IMPLEMENTATION REQUIRED ─────────────────────────────────────────
    /// This method is called every time NotifyAsync is invoked.
    /// Add your delivery logic below — the in-memory store already handles the
    /// bell/badge in the UI; this method is for external delivery.
    ///
    /// Examples:
    ///
    ///   SignalR (real-time browser push):
    ///     await _hubContext.Clients.All.SendAsync("ReceiveNotification", entry);
    ///
    ///   Email:
    ///     await _mailer.SendAsync("admin@example.com", entry.Title, entry.Message);
    ///
    ///   Slack webhook:
    ///     await _http.PostAsJsonAsync(slackWebhookUrl,
    ///         new { text = $"[{entry.Level}] {entry.Title}: {entry.Message}" });
    ///
    ///   Azure Service Bus / RabbitMQ:
    ///     await _bus.PublishAsync(entry);
    /// ─────────────────────────────────────────────────────────────────────────
    private static Task DeliverAsync(NotificationEntry entry)
    {
        // Add your delivery code here.
        return Task.CompletedTask;
    }

    public IReadOnlyList<NotificationEntry> GetRecent(int count = 50)
        => _store.Reverse().Take(count).ToList();

    public int GetUnreadCount()
        => _store.Count(n => !n.IsRead);

    public void MarkAllRead()
    {
        foreach (var n in _store)
            n.IsRead = true;
    }
}
