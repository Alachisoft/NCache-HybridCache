using HybridCachePlayground.Web.Models;
using Microsoft.Extensions.Caching.Hybrid;

namespace HybridCachePlayground.Web.Services;

public interface ICachePlaygroundService
{
    Task SetAsync(string key, string value, IEnumerable<string> tags, int expirationMinutes,
        HybridCacheEntryFlags flags = HybridCacheEntryFlags.None, CancellationToken ct = default);

    Task<BulkSetResult> BulkSetAsync(string keyPrefix, int count, IEnumerable<string> tags,
        int expirationMinutes, HybridCacheEntryFlags flags = HybridCacheEntryFlags.None,
        CancellationToken ct = default);

    Task<CacheGetResult> GetOrCreateAsync(string key,
        HybridCacheEntryFlags flags = HybridCacheEntryFlags.None,
        int factoryTemplateIndex = -1,
        IEnumerable<string>? tags = null,
        string? factoryValue = null,
        CancellationToken ct = default);

    Task<StampedeResult> RunStampedeTestAsync(string key, int concurrency, bool forceEvict, CancellationToken ct = default);

    Task<ConcurrentGetResult> RunConcurrentGetTestAsync(string key, int concurrency, int factoryDelayMs, bool forceEvict, CancellationToken ct = default);

    Task RemoveAsync(string key, CancellationToken ct = default);

    Task RemoveAsync(IEnumerable<string> keys, CancellationToken ct = default);

    Task RemoveByTagAsync(string tag, CancellationToken ct = default);

    Task RemoveByTagAsync(IEnumerable<string> tags, CancellationToken ct = default);

    /// <summary>
    /// Removes all entries whose tags match a glob pattern (* = any chars, ? = single char).
    /// Returns the number of entries removed and the list of matched tags.
    /// </summary>
    Task RemoveByTagWildcardAsync(string pattern, CancellationToken ct = default);


    IReadOnlyList<CacheEntryMetadata> GetAllEntries();

    CacheStats GetStats();

    IReadOnlyList<KeyRegistryEntry> GetKeyRegistry();

    IReadOnlyList<TagRegistryEntry> GetTagRegistry();

    void PruneExpired();

    /// <summary>Reset hit / miss / factory invocation counters to zero.</summary>
    void ResetStatistics();
}
