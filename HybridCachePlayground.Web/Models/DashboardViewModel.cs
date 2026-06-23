namespace HybridCachePlayground.Web.Models;

public class DashboardViewModel
{
    public List<CacheEntryMetadata> Entries { get; set; } = [];
    public CacheStats Stats { get; set; } = new();
    public List<KeyRegistryEntry> KeyRegistry { get; set; } = [];
    public List<TagRegistryEntry> TagRegistry { get; set; } = [];
    public int TotalNotifications { get; set; }
    public int UnreadNotifications { get; set; }
    public InstanceInfo? Instance { get; set; }
}
