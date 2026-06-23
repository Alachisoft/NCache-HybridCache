namespace HybridCachePlayground.Web.Models;
public class ConcurrentGetResult
{
    public string Key { get; set; } = string.Empty;
    public int RequestCount { get; set; }
    public int FactoryInvocations { get; set; }
    public int CacheHits { get; set; }
    public int SuccessfulResponses { get; set; }
    public long ElapsedMs { get; set; }
    public int FactoryDelayMs { get; set; }
    public string? SampleValue { get; set; }
    public bool WasEvicted { get; set; }
    public string Verdict => FactoryInvocations switch {
        0 => "All requests were served from cache — no factory invocations.",
        1 => $"Factory ran once for {RequestCount} concurrent requests. HybridCache coalesced them correctly.",
        _ => $"Factory ran {FactoryInvocations} times — unexpected for {RequestCount} concurrent requests."
    };
}
