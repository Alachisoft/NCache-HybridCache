namespace HybridCachePlayground.Web.Models;

public class StampedeResult
{
    public string Key { get; set; } = string.Empty;
    public int RequestCount { get; set; }
    public int FactoryInvocations { get; set; }
    public int SuccessfulResponses { get; set; }
    public long ElapsedMs { get; set; }
    public string? SampleValue { get; set; }

    /// <summary>True when HybridCache coalesced all requests — factory ran exactly once.</summary>
    public bool StampedeProtectionWorked => FactoryInvocations == 1;

    public string Verdict => FactoryInvocations switch
    {
        0 => "All requests were cache HITs — evict the key first to see stampede protection.",
        1 => $"Stampede protection worked. {RequestCount} concurrent requests, factory ran exactly 1 time.",
        _ => $"Factory ran {FactoryInvocations} times for {RequestCount} requests — protection did not fully coalesce."
    };
}
