# NCache.Microsoft.Extensions.Caching.Hybrid.Opensource

**NCache Open Source implementation for ASP.NET Core HybridCache** providing Local L1 and distributed L2 caching with tag-based invalidation and NCache Pub/Sub synchronization.

## Package Versions

| Package | Version |
|---------|---------|
| NCache.OSS.Microsoft.Extensions.Caching.Hybrid | 5.3.6.1 |
| Alachisoft.NCache.Opensource.SDK | >= 5.3.6.2 |
| Microsoft.Extensions.Caching.Hybrid | >= 10.4.0 |


## Features

- **L1/L2 Hybrid Caching** – Blazing fast local in-memory cache (L1) backed by distributed NCache (L2)
- **Tag-Based Invalidation** – Invalidate groups of related cache entries using tags
- **Bulk Operations** – Remove multiple keys or invalidate multiple tags in a single operation
- **Pub/Sub Synchronization** – Automatic L1 cache synchronization across all application nodes
- **Cache Stampede Prevention** – Built-in semaphore-based locking to prevent thundering herd
- **Microsoft HybridCache Compatible** – Drop-in replacement implementing `Microsoft.Extensions.Caching.Hybrid.HybridCache`
- **Configurable Cache Flags** – Fine-grained control over L1/L2 read/write operations
- **Structured Logging** – Full integration with `Microsoft.Extensions.Logging`

## Installation

Install via NuGet Package Manager:

```bash
dotnet add package NCache.OSS.Microsoft.Extensions.Caching.Hybrid
```

Or via Package Manager Console:

```powershell
Install-Package NCache.OSS.Microsoft.Extensions.Caching.Hybrid
```

## Prerequisites

Before using this package, ensure you have:

1. **NCache Server** – A running NCache cluster (Open Source or Enterprise)
2. **Local Cache** – An In-Proc cache configured for L1 caching
3. **Distributed Cache** – A Replicated/Partitioned cache configured for L2 caching

## Quick Start

### 1. Configure `appsettings.json`

```json
{
  "NCacheHybridCacheConfiguration": {
    "LocalCacheName": "myLocalCache",
    "DistributedCacheName": "myDistributedCache",
    "ServerList": [
      {
        "Ip": "192.168.1.100",
        "Port": 9800
      },
      {
        "Ip": "192.168.1.101",
        "Port": 9800
      }
    ],
    "EnableLogs": true
  }
}
```

### 2. Register Services in `Program.cs`

**Using IConfiguration:**

```csharp
var builder = WebApplication.CreateBuilder(args);

// Option 1: Bind from configuration section
var config = builder.Configuration.GetSection("NCacheHybridCacheConfiguration");
builder.Services.AddNCacheHybridCache(config);

// Option 2: Bind directly (auto-detects section name)
builder.Services.AddNCacheHybridCache(builder.Configuration);
```

**Using Action delegate:**

```csharp
builder.Services.AddNCacheHybridCache(options =>
{
    options.LocalCacheName = "myLocalCache";
    options.DistributedCacheName = "myDistributedCache";
    options.ServerList = new List<ServerConfig>
    {
        new ServerConfig { Ip = "192.168.1.100", Port = 9800 }
    };
    options.EnableLogs = true;
});
```

### 3. Inject and Use `HybridCache`

```csharp
public class ProductService
{
    private readonly HybridCache _cache;

    public ProductService(HybridCache cache)
    {
        _cache = cache;
    }

    public async Task<Product> GetProductAsync(int productId)
    {
        return await _cache.GetOrCreateAsync(
            key: $"product:{productId}",
            state: productId,
            factory: async (id, ct) => await _database.GetProductAsync(id, ct),
            tags: new[] { "products", $"category:{product.CategoryId}" }
        );
    }
}
```

## API Reference

### GetOrCreateAsync

Retrieves a value from cache or creates it using the factory if not present.

```csharp
ValueTask<T> GetOrCreateAsync<TState, T>(
    string key,
    TState state,
    Func<TState, CancellationToken, ValueTask<T>> factory,
    HybridCacheEntryOptions? options = null,
    IEnumerable<string>? tags = null,
    CancellationToken cancellationToken = default
);
```

**Parameters:**
- `key` – Unique cache key identifier (null/empty keys are handled gracefully)
- `state` – State passed to the factory function
- `factory` – Async function to generate the value on cache miss
- `options` – Optional cache entry settings (expiration, flags)
- `tags` – Optional tags for grouping/invalidation
- `cancellationToken` – Cancellation token passed to factory and semaphore

**Behavior:**
- **Null/empty key handling**: If key is null/empty and `DisableUnderlyingData` flag is set, returns `default(T)`. Otherwise, invokes factory without caching.
- **L1 cache check**: Returns cached value via `item.GetValue<T>()` if tags are valid. If tags are invalid, sets `isItemInvalid` flag and skips L2 check.
- **L2 cache check**: Skipped if `isItemInvalid` is true. On hit with valid tags, populates L1 with `LocalCacheExpiration` from options or config.
- **Stampede prevention**: Uses semaphore lock with `cancellationToken`. TRY-FINALLY block ensures cleanup.
- **Factory invocation**: Passes `cancellationToken` to factory function.
- **Cache population**: L2 populated first (with `Expiration` from options or config), then L1 (with `LocalCacheExpiration`).

**Example:**

```csharp
var user = await _cache.GetOrCreateAsync(
    key: $"user:{userId}",
    state: userId,
    factory: async (id, ct) => await _userRepository.GetByIdAsync(id, ct),
    options: new HybridCacheEntryOptions
    {
        Expiration = TimeSpan.FromMinutes(30),         // L2 expiration
        LocalCacheExpiration = TimeSpan.FromMinutes(5)  // L1 expiration (shorter)
    },
    tags: new[] { "users", $"tenant:{tenantId}" },
    cancellationToken: cancellationToken
);
```

### SetAsync

Explicitly sets a value in both L1 and L2 caches.

```csharp
ValueTask SetAsync<T>(
    string key,
    T value,
    HybridCacheEntryOptions? options = null,
    IEnumerable<string>? tags = null,
    CancellationToken cancellationToken = default
);
```

**Behavior:**
- **Input validation**: Throws `ArgumentNullException` if key or value is null/empty.
- **L2 write first**: Item is stored in L2 (distributed) cache with `Expiration` from options or config default.
- **Pub/Sub notification**: UPDATE message published after L2 write to sync other nodes.
- **L1 write**: Item is stored in L1 (local) cache with `LocalCacheExpiration` from options or config default.

**Example:**

```csharp
await _cache.SetAsync(
    key: $"config:{configKey}",
    value: configValue,
    options: new HybridCacheEntryOptions
    {
        Expiration = TimeSpan.FromHours(1),
        LocalCacheExpiration = TimeSpan.FromMinutes(10)
    },
    tags: new[] { "configuration" }
);
```

### RemoveAsync (Single Key)

Removes a specific cache entry by key from both L1 and L2 caches.

```csharp
ValueTask RemoveAsync(string key, CancellationToken cancellationToken = default);
```

**Behavior:**
- **Null/empty key handling**: Returns early without throwing exceptions if key is null/empty.
- **L1 removal first**: Item removed from L1 (local) cache.
- **L2 removal**: Item removed from L2 (distributed) cache.
- **Pub/Sub notification**: REMOVE message published to sync other nodes.

**Example:**

```csharp
await _cache.RemoveAsync($"product:{productId}");
```

### RemoveAsync (Bulk Keys)

Removes multiple cache entries by keys from both L1 and L2 caches in a single operation.

```csharp
ValueTask RemoveAsync(IEnumerable<string> keys, CancellationToken cancellationToken = default);
```

**Behavior:**
- **Null/empty collection handling**: Returns early if keys collection is null or empty.
- **Bulk L1 removal**: All keys removed from L1 using `RemoveBulk` for efficiency.
- **Bulk L2 removal**: All keys removed from L2 using `RemoveBulk` for efficiency.
- **Single Pub/Sub notification**: One REMOVE message containing all keys published to sync other nodes.

**Example:**

```csharp
// Remove multiple products at once
var keysToRemove = new[] 
{ 
    "product:1", 
    "product:2", 
    "product:3" 
};
await _cache.RemoveAsync(keysToRemove);
```

### RemoveByTagAsync (Single Tag)

Invalidates all cache entries associated with a specific tag using **logical deletion**. This method does not physically remove cache entries; instead, it marks the tag as invalid by updating its timestamp in TagKeeper, causing future cache lookups to treat associated entries as invalidated.

```csharp
ValueTask RemoveByTagAsync(string tag, CancellationToken cancellationToken = default);
```

**Behavior:**
- **Null/empty/whitespace tag handling**: Returns early without throwing exceptions.
- **Timestamp recording**: Current Unix timestamp stored in `TagKeeper._tagInvalidationTimes[tag]`.
- **L2 sentinel storage**: Invalidation timestamp stored in L2 with key `"$$sentinel$$:{tag}"` for cross-node persistence.
- **Pub/Sub notification**: TAG message published to sync tag invalidation to all nodes.
- **Logical deletion**: Cache entries remain physically stored in L1/L2 but are treated as invalid during `GetOrCreateAsync` if `item.CreatedTimeStamp < tag invalidation timestamp`.
- **Wildcard support**: Using `"*"` as tag performs global invalidation, clearing all `_tagInvalidationTimes` and setting `_globalInvalidationTimestamp`. Sentinel stored as `"$$sentinel$$:*"`.

**Example:**

```csharp
// Invalidate all products in a category
await _cache.RemoveByTagAsync($"category:{categoryId}");

// Invalidate all user-related cache entries
await _cache.RemoveByTagAsync("users");

// Wildcard: Invalidate ALL cached entries
await _cache.RemoveByTagAsync("*");
```

> **Note:** Cache entries remain physically stored in L1 and L2 until their expiration time. However, they are treated as invalid during retrieval, triggering factory re-execution in `GetOrCreateAsync`. This approach provides efficient invalidation without the overhead of physically removing entries from distributed caches.

### RemoveByTagAsync (Bulk Tags)

Invalidates all cache entries associated with multiple tags in a single operation using **logical deletion**. Similar to the single-tag variant, this method marks all specified tags as invalid via timestamp updates without physically removing cache entries.

```csharp
ValueTask RemoveByTagAsync(IEnumerable<string> tags, CancellationToken cancellationToken = default);
```

**Behavior:**
- **Null/empty collection handling**: Returns early if tags collection is null or empty.
- **Batch timestamp recording**: Each tag's invalidation timestamp stored in `TagKeeper._tagInvalidationTimes`.
- **Batch L2 sentinel storage**: All tag sentinels (format `"$$sentinel$$:{tag}"`) stored in L2 for persistence.
- **Single Pub/Sub notification**: One TAG message containing all tags published to sync all nodes efficiently.
- **Logical deletion**: All cache entries associated with any of the tags are marked invalid without physical removal.

**Example:**

```csharp
// Invalidate multiple categories at once
var tagsToInvalidate = new[] 
{ 
    "category:electronics", 
    "category:furniture", 
    "category:clothing" 
};
await _cache.RemoveByTagAsync(tagsToInvalidate);

// Invalidate all entities of specific types
await _cache.RemoveByTagAsync(new[] { "products", "users", "orders" });
```

> **Note:** Bulk tag invalidation is more efficient than individual invalidations as it updates multiple tag timestamps in a single L2 operation and publishes a single Pub/Sub notification. Cache entries remain physically stored but are treated as invalid during retrieval.

## Configuration Options

### NCacheHybridCacheConfiguration

| Property | Type | Required | Default | Description |
|----------|------|----------|---------|-------------|
| `LocalCacheName` | `string` | ✅ Yes | - | Name of the NCache In-Proc cache (L1) |
| `DistributedCacheName` | `string` | ✅ Yes | - | Name of the NCache distributed cache (L2) |
| `ServerList` | `IList<ServerConfig>` | ✅ Yes | - | List of NCache L2 server nodes |
| `EnableLogs` | `bool` | No | `false` | Enable detailed logging |

### ServerConfig

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Ip` | `string` | - | IP address of the NCache server |
| `Port` | `int` | `9800` | Port number of the NCache server |

### HybridCacheEntryOptions

| Property | Type | Description |
|----------|------|-------------|
| `Expiration` | `TimeSpan?` | Distributed cache (L2) entry expiration. If null, uses `NCacheHybridCacheConfiguration.DefaultEntryOptions.Expiration` |
| `LocalCacheExpiration` | `TimeSpan?` | Local cache (L1) entry expiration. If null, uses `NCacheHybridCacheConfiguration.LocalCacheExpiration` |
| `Flags` | `HybridCacheEntryFlags` | Cache behavior flags |

### HybridCacheEntryFlags

| Flag | Description |
|------|-------------|
| `None` | Default behavior – read/write both caches |
| `DisableLocalCacheRead` | Skip L1 cache reads |
| `DisableLocalCacheWrite` | Skip L1 cache writes |
| `DisableLocalCache` | Skip both L1 reads and writes |
| `DisableDistributedCacheRead` | Skip L2 cache reads |
| `DisableDistributedCacheWrite` | Skip L2 cache writes |
| `DisableDistributedCache` | Skip both L2 reads and writes |
| `DisableUnderlyingData` | Return `default(T)` instead of invoking factory when key is null/empty |

## Architecture

```
┌────────────────────────────────────────────────────────────┐
│                        Application                         │
│                                                            │
│  ┌──────────────┐    ┌──────────────┐    ┌──────────────┐  │
│  │   App Node   │    │   App Node   │    │   App Node   │  │
│  │              │    │              │    │              │  │
│  │  ┌────────┐  │    │  ┌────────┐  │    │  ┌────────┐  │  │
│  │  │L1 Cache│  │    │  │L1 Cache│  │    │  │L1 Cache│  │  │
│  │  │(InProc)│  │    │  │(InProc)│  │    │  │(InProc)│  │  │
│  │  └───┬────┘  │    │  └───┬────┘  │    │  └───┬────┘  │  │
│  └──────┼───────┘    └──────┼───────┘    └──────┼───────┘  │
│         │                   │                   │          │
│         └───────────────────┼───────────────────┘          │
│                             │ Pub/Sub Sync                 │
└─────────────────────────────┼──────────────────────────────┘
                              │
              ┌───────────────┴────────────┐
              │     NCache Distributed     │
              │       Cluster (L2)         │
              │  ┌─────────┐  ┌─────────┐  │
              │  │ Server  │  │ Server  │  │
              │  │   1     │  │   2     │  │
              │  └─────────┘  └─────────┘  │
              └────────────────────────────┘
```

### Cache Flow

1. **Read Operations (GetOrCreateAsync):**
   - **Null key check**: If key is null/empty, either return `default(T)` (if `DisableUnderlyingData` set) or invoke factory without caching
   - **L1 check**: Get from local cache, validate tags via TagKeeper. If valid, return `item.GetValue<T>()`. If invalid, set `isItemInvalid=true`
   - **L2 check**: Only if `!isItemInvalid`, get from distributed cache, validate tags. On hit, populate L1 with `LocalCacheExpiration` (options or config)
   - **Stampede prevention**: Acquire semaphore lock with `cancellationToken`, double-check L1, ensure cleanup in TRY-FINALLY
   - **Factory invocation**: Call `factory(state, cancellationToken)` if cache miss or invalid tags
   - **Cache population**: Store in L2 first (with `Expiration`), then L1 (with `LocalCacheExpiration`)

2. **Write Operations (SetAsync):**
   - **Input validation**: Throw `ArgumentNullException` if key or value is null
   - **L2 write**: Store in distributed cache with `Expiration` (options or config default)
   - **Pub/Sub notification**: Publish UPDATE message after L2 write
   - **L1 write**: Store in local cache with `LocalCacheExpiration` (options or config default)

3. **Remove Operations (RemoveAsync):**
   - **Null/empty key handling**: Return early without exception
   - **L1 removal**: Remove from local cache (or `RemoveBulk` for multiple keys)
   - **L2 removal**: Remove from distributed cache (or `RemoveBulk` for multiple keys)
   - **Pub/Sub notification**: Publish REMOVE message with key(s)

4. **Tag Invalidation (RemoveByTagAsync - Logical Deletion):**
   - **Null/empty tag handling**: Return early without exception
   - **Timestamp recording**: Store current Unix timestamp in `TagKeeper._tagInvalidationTimes[tag]`
   - **L2 sentinel storage**: Store timestamp in L2 with key `"$$sentinel$$:{tag}"` (or `"$$sentinel$$:*"` for wildcard)
   - **Wildcard handling**: If tag is `"*"`, set `_globalInvalidationTimestamp`, clear all `_tagInvalidationTimes`
   - **Pub/Sub notification**: Broadcast TAG or WILDCARD message to all nodes
   - **Cross-node sync**: All nodes update local `_tagInvalidationTimes` from L2 sentinels
   - **Physical storage**: Cache entries remain in L1/L2, marked invalid only via timestamps

## Best Practices

### Bulk Operations Efficiency

```csharp
var keysToRemove = products.Select(p => $"product:{p.Id}");
await _cache.RemoveAsync(keysToRemove);

foreach (var product in products)
{
    await _cache.RemoveAsync($"product:{product.Id}"); // Less efficient
}
```

```csharp
var tagsToInvalidate = new[] { "category:1", "category:2", "category:3" };
await _cache.RemoveByTagAsync(tagsToInvalidate);

await _cache.RemoveByTagAsync("category:1");
await _cache.RemoveByTagAsync("category:2");
await _cache.RemoveByTagAsync("category:3");
```

### Tagging Strategy

```csharp
// Use hierarchical tags for flexible invalidation
var tags = new[] 
{
    "entity:product",              // Invalidate all products
    $"category:{categoryId}",      // Invalidate by category
    $"tenant:{tenantId}",          // Invalidate by tenant
    $"product:{productId}"         // Invalidate specific product
};
```

### Expiration Guidelines

```csharp
var options = new HybridCacheEntryOptions
{
    // L2 expiration should be longer (source of truth)
    Expiration = TimeSpan.FromHours(1),

    // L1 expiration should be shorter (quick refresh)
    LocalCacheExpiration = TimeSpan.FromMinutes(5)
};
```

### Selective Cache Layers

```csharp
// Read-heavy, rarely changing data – prefer L1
var options = new HybridCacheEntryOptions
{
    LocalCacheExpiration = TimeSpan.FromMinutes(30)
};

// Frequently changing shared data – skip L1
var options = new HybridCacheEntryOptions
{
    Flags = HybridCacheEntryFlags.DisableLocalCache
};
```

## Troubleshooting

### Common Issues

| Issue | Cause | Solution |
|-------|-------|----------|
| `InvalidOperationException` on startup | Invalid configuration | Verify `LocalCacheName`, `DistributedCacheName`, and `ServerList` |
| Connection timeout | Server unreachable | Check NCache server status and firewall rules |
| Data inconsistency across nodes | Pub/Sub not working | Verify messaging topic is created and accessible |
| Null key behavior | Unexpected null key handling | Check `DisableUnderlyingData` flag: if set, returns `default(T)`; otherwise invokes factory |
| Stale data after tag invalidation | Tag validation issue | Verify sentinel keys `"$$sentinel$$:{tag}"` exist in L2 cache |

### Enable Diagnostic Logging

```json
{
  "Logging": {
    "LogLevel": {
      "NCache.Microsoft.Extensions.Caching.Hybrid.Opensource": "Debug"
    }
  },
  "NCacheHybridCacheConfiguration": {
    "EnableLogs": true
  }
}
```


### Key Optimizations

- **isItemInvalid Flag**: When L1 cache hit has invalid tags, L2 read is skipped and factory is invoked directly
- **TRY-FINALLY Cleanup**: Semaphore locks are always released using TRY-FINALLY blocks, even on exceptions
- **Expiration Precedence**: `options.Expiration` overrides `config.DefaultEntryOptions.Expiration` for L2; `options.LocalCacheExpiration` overrides `config.LocalCacheExpiration` for L1
- **Bulk Operations**: `RemoveBulk` and `GetBulk` used for efficiency when operating on multiple keys/tags
- **Sentinel Keys**: Tag invalidation timestamps persisted in L2 with format `"$$sentinel$$:{tag}"` or `"$$sentinel$$:*"` for wildcard


## License

Copyright © 2005-2026 Alachisoft. All rights reserved.

## Resources

- [NCache Documentation](https://www.alachisoft.com/resources/docs/)
- [NCache Open Source](https://github.com/Alachisoft/NCache)
- [Microsoft HybridCache Documentation](https://learn.microsoft.com/en-us/aspnet/core/performance/caching/hybrid)
- [Alachisoft Website](https://www.alachisoft.com/ncache/)
