namespace HybridCachePlayground.Web.Models;

public class KeyRegistryEntry
{
    public string Key { get; set; } = string.Empty;
    public DateTimeOffset FirstSeen { get; set; }
    public DateTimeOffset LastSeen { get; set; }
    public int TimesSet { get; set; }
    public long Hits;   // backing field — updated via Interlocked
    public long Misses; // backing field — updated via Interlocked
    public List<string> LastKnownTags { get; set; } = [];
    public bool IsCurrentlyActive { get; set; }

    public double HitRatio => (Hits + Misses) == 0 ? 0
        : Math.Round((double)Hits / (Hits + Misses) * 100, 1);
}
