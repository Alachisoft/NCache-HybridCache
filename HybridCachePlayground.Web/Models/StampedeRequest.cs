using System.ComponentModel.DataAnnotations;

namespace HybridCachePlayground.Web.Models;

public class StampedeRequest
{
    [Required(ErrorMessage = "Key is required")]
    public string Key { get; set; } = "stampede-test";

    /// <summary>Upper bound enforced server-side against HybridCache:StampedeMaxConcurrency config.</summary>
    [Range(2, 1000, ErrorMessage = "Concurrency must be at least 2")]
    public int Concurrency { get; set; } = 20;

    /// <summary>When true the key is evicted from cache before firing, guaranteeing a cold start.</summary>
    public bool ForceEvict { get; set; } = true;
}
