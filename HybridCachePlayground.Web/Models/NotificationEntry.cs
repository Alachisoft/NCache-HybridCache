namespace HybridCachePlayground.Web.Models;

public enum NotificationLevel { Info, Success, Warning, Error }

public class NotificationEntry
{
    public Guid            Id        { get; init; } = Guid.NewGuid();
    public string          Title     { get; set; }  = string.Empty;
    public string          Message   { get; set; }  = string.Empty;
    public NotificationLevel Level   { get; set; }  = NotificationLevel.Info;
    public DateTimeOffset  Timestamp { get; init; } = DateTimeOffset.UtcNow;
    public bool            IsRead    { get; set; }

    public string BadgeClass => Level switch
    {
        NotificationLevel.Success => "text-bg-success",
        NotificationLevel.Warning => "text-bg-warning",
        NotificationLevel.Error   => "text-bg-danger",
        _                         => "text-bg-info"
    };
}
