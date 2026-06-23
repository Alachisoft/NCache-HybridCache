using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Caching.Hybrid;

namespace HybridCachePlayground.Web.Models;

public class CacheGetRequest
{
    [Required(ErrorMessage = "Key is required")]
    public string Key { get; set; } = string.Empty;

    /// <summary>Factory template index. -1 = random.</summary>
    public int FactoryTemplateIndex { get; set; } = -1;

    /// <summary>Optional pre-generated (and possibly edited) factory value. Used on cache miss.</summary>
    public string? FactoryValue { get; set; }

    /// <summary>Comma-separated tags to attach when the factory stores the value on miss.</summary>
    public string? TagsRaw { get; set; }

    public List<string> ParsedTags => string.IsNullOrWhiteSpace(TagsRaw)
        ? []
        : TagsRaw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                 .Distinct()
                 .ToList();

    // ── HybridCacheEntryFlags ────────────────────────────────────────────────
    public bool DisableLocalCacheRead       { get; set; }
    public bool DisableDistributedCacheRead { get; set; }
    public bool DisableLocalCacheWrite      { get; set; }
    public bool DisableDistributedCacheWrite{ get; set; }

    public HybridCacheEntryFlags GetEntryFlags()
    {
        var f = HybridCacheEntryFlags.None;
        if (DisableLocalCacheRead)        f |= HybridCacheEntryFlags.DisableLocalCacheRead;
        if (DisableDistributedCacheRead)  f |= HybridCacheEntryFlags.DisableDistributedCacheRead;
        if (DisableLocalCacheWrite)       f |= HybridCacheEntryFlags.DisableLocalCacheWrite;
        if (DisableDistributedCacheWrite) f |= HybridCacheEntryFlags.DisableDistributedCacheWrite;
        return f;
    }
}
