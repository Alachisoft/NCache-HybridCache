using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Caching.Hybrid;

namespace HybridCachePlayground.Web.Models;

public class CacheSetRequest
{
    [Required(ErrorMessage = "Key is required")]
    [StringLength(200, MinimumLength = 1)]
    public string Key { get; set; } = string.Empty;

    [Required(ErrorMessage = "Value is required")]
    public string Value { get; set; } = string.Empty;

    /// <summary>Comma-separated list of tags.</summary>
    public string? Tags { get; set; }

    [Range(1, 1440, ErrorMessage = "Expiration must be between 1 and 1440 minutes")]
    public int? ExpirationMinutes { get; set; }

    // ── HybridCacheEntryFlags (write-side only for SetAsync) ─────────────────
    public bool DisableLocalCacheWrite       { get; set; }
    public bool DisableDistributedCacheWrite { get; set; }

    public HybridCacheEntryFlags GetEntryFlags()
    {
        var f = HybridCacheEntryFlags.None;
        if (DisableLocalCacheWrite)       f |= HybridCacheEntryFlags.DisableLocalCacheWrite;
        if (DisableDistributedCacheWrite) f |= HybridCacheEntryFlags.DisableDistributedCacheWrite;
        return f;
    }

    public List<string> ParsedTags =>
        string.IsNullOrWhiteSpace(Tags)
            ? []
            : Tags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
}
