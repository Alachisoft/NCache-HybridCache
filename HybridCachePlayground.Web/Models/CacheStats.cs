namespace HybridCachePlayground.Web.Models;

public class CacheStats
{
    public long Hits { get; set; }
    public long Misses { get; set; }
    public int TotalTrackedEntries { get; set; }
    public int ActiveEntries { get; set; }
    public int TotalUniqueKeys { get; set; }
    public int TotalUniqueTags { get; set; }
    public long FactoryInvocations { get; set; }

    public double HitRatio => (Hits + Misses) == 0 ? 0 : Math.Round((double)Hits / (Hits + Misses) * 100, 1);
}
