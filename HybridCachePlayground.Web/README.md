# HybridCachePlayground.Web

An interactive ASP.NET Core 8 playground for exploring **NCache HybridCache** — the two-tier caching layer that pairs an in-process local cache (`demoLocalCache`) with a distributed remote cache (`demoCache`). The app lets you set, get, remove, and tag entries while observing hit/miss statistics, stampede protection, and tag-based invalidation in real time.

---

## ⚠️ Prerequisites — NCache Servers Must Be Running

The application will **fail to start** if either NCache cache is unreachable. Both must be running and accessible before you launch the project.

| Configuration key | Cache name | Purpose |
|---|---|---|
| `NCacheHybridCache:DistributedCacheName` | **`demoCache`** | Remote / distributed L2 cache |
| `NCacheHybridCache:LocalCacheName` | **`demoLocalCache`** | In-process / local L1 cache |

Both caches are expected on the server at `10.0.5.1:9800` (configured in `client.ncconf`). Update that file if your NCache server lives at a different address.

---

## Getting Started

### 1. Verify NCache is Running

On your NCache server, confirm both caches are started:

```
demoCache       → must be running
demoLocalCache  → must be running
```

You can check via the NCache Manager, the NCache Web Manager, or the CLI:

```bash
list-caches -server 10.0.5.1
```

If either cache is stopped, start it before proceeding.

### 2. Update `client.ncconf` (if needed)

Open `HybridCachePlayground.Web/client.ncconf` and make sure the server IP matches your environment:

```xml
<cache id="demoCache" ...>
    <server name="10.0.5.1"/>
</cache>
<cache id="demoLocalCache" ...>
    <server name="10.0.5.1"/>
</cache>
```

### 3. Restore & Run

```bash
dotnet restore
dotnet run --project HybridCachePlayground.Web
```

Or open the solution in Visual Studio and press **F5**.

The app will be available at:

- HTTP — `http://localhost:5280`
- HTTPS — `https://localhost:7020`

---

## Project Structure

```
HybridCachePlayground.Web/
├── Controllers/
│   ├── CacheController.cs      # Set, Get, Remove, BulkSet, Stampede, ConcurrentGet
│   ├── HomeController.cs       # Dashboard with live stats
│   └── ToolsController.cs      # Debug tools and log viewer
├── Services/
│   ├── CachePlaygroundService  # Core HybridCache wrapper (hit/miss tracking, tag registry)
│   ├── DebugToolsService       # Internal diagnostics
│   └── NotificationService     # Real-time UI notifications
├── Models/                     # Request/response view models
├── Views/                      # Razor views for each operation
├── appsettings.json            # Instance config, cache names, Serilog settings
├── client.ncconf               # NCache client connection config (server IP, port, cache IDs)
└── logs/                       # Per-session log files (auto-created on startup)
```

---

## Features

**Cache Operations**
- **Set** — Store a key/value with a TTL, tags, and optional cache flags
- **Get / GetOrCreate** — Retrieve a key, with optional factory invocation simulation
- **Remove** — Delete a single key or a list of keys
- **Bulk Set** — Seed the cache with many entries using a key prefix
- **Remove by Tag** — Invalidate all entries sharing a tag (exact or wildcard match)

**Concurrency & Stampede Protection**
- **Stampede Test** — Fire N simultaneous `GetOrCreate` calls against the same key to demonstrate NCache's single-factory guarantee
- **Concurrent Get Test** — Configurable concurrency with a simulated factory delay

**Observability**
- Live hit/miss/factory-invocation counters on the dashboard
- Per-session structured log files written to `logs/` (Serilog)
- In-app log viewer under **Tools → Logs**
- Key registry and tag registry showing all entries ever written in the current session

---

## Configuration Reference

`appsettings.json` key settings:

```jsonc
{
  "Instance": {
    "Id": "instance-1",       // Shown in the dashboard — useful when running multiple instances
    "Color": "#4d9ef7"        // Accent color for this instance's UI
  },
  "NCacheHybridCache": {
    "LocalCacheName": "demoLocalCache",    // ← L1 in-process cache (must be running)
    "DistributedCacheName": "demoCache"    // ← L2 distributed cache (must be running)
  }
}
```

Default TTLs can be overridden in `appsettings.json`:

```jsonc
"HybridCache": {
  "DefaultExpirationMinutes": 5,       // Distributed cache TTL
  "LocalCacheExpirationMinutes": 2,    // Local cache TTL
  "StampedeMaxConcurrency": 200        // Upper limit for stampede test
}
```

---

## NuGet Dependencies

| Package | Version |
|---|---|
| `Alachisoft.NCache.Opensource.SDK` | 5.3.6.2 |
| `NCache.OSS.Microsoft.Extensions.Caching.Hybrid` | 5.3.6.1 |
| `Microsoft.Extensions.Caching.Hybrid` | 10.5.0 |
| `Microsoft.Extensions.Caching.Memory` | 10.0.6 |
| `Serilog.AspNetCore` | 10.0.0 |

---

## Troubleshooting

**App crashes immediately on startup**
Both NCache caches must be running before the app starts. See the Prerequisites section above.

**"Cache not found" or connection errors**
Check the server IP and port in `client.ncconf`. The default is `10.0.5.1:9800`.

**Factory is being called on every Get**
This is expected if the local cache TTL has expired but the distributed cache still holds the value — the entry will be promoted back to L1 after the first factory call. Use the **Stats** panel to distinguish local misses from distributed misses.

**Log files not appearing**
The `logs/` directory is created automatically on startup. Each run writes its own timestamped file (`hybridcache-<timestamp>.log`). Check file-system permissions if the directory is missing.
