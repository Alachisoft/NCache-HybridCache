namespace HybridCachePlayground.Web.Models;

public class CacheEntryMetadata
{
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = [];
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? LastAccessedAt { get; set; }
    public bool FactoryGenerated { get; set; }
    public string? FactoryLabel { get; set; }

    public bool IsExpired => DateTimeOffset.UtcNow > ExpiresAt;

    public string ExpiresIn
    {
        get
        {
            var remaining = ExpiresAt - DateTimeOffset.UtcNow;
            if (remaining <= TimeSpan.Zero) return "Expired";
            if (remaining.TotalHours >= 1) return $"{(int)remaining.TotalHours}h {remaining.Minutes}m";
            if (remaining.TotalMinutes >= 1) return $"{(int)remaining.TotalMinutes}m {remaining.Seconds}s";
            return $"{(int)remaining.TotalSeconds}s";
        }
    }
}
