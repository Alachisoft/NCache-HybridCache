namespace HybridCachePlayground.Web.Services;

public interface IDebugToolsService
{
    /// <summary>Clear L1 (in-process) cache. Stub — user implements.</summary>
    Task ClearL1CacheAsync();

    /// <summary>Clear L2 (distributed) cache. Stub — user implements.</summary>
    Task ClearL2CacheAsync();

    /// <summary>Reset hit / miss / factory invocation counters.</summary>
    Task ResetStatisticsAsync();

    /// <summary>Force-prune all expired metadata entries.</summary>
    void PruneExpiredEntries();

    /// <summary>Generate and store N random entries for pressure testing.</summary>
    Task<int> SimulatePressureAsync(string keyPrefix, int entryCount, CancellationToken ct = default);

    /// <summary>Export current cache state as a formatted JSON string.</summary>
    Task<string> ExportSnapshotAsync();

    /// <summary>Write a full structured cache-state dump to the log.</summary>
    void LogCacheState();
}
