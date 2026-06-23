using System.Diagnostics;
using HybridCachePlayground.Web.Models;
using HybridCachePlayground.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace HybridCachePlayground.Web.Controllers;

public class HomeController : Controller
{
    private readonly ICachePlaygroundService _cacheService;
    private readonly INotificationService _notifications;
    private readonly ILogger<HomeController> _logger;
    private readonly InstanceInfo _instanceInfo;

    public HomeController(
        ICachePlaygroundService cacheService,
        INotificationService notifications,
        ILogger<HomeController> logger,
        InstanceInfo instanceInfo)
    {
        _cacheService  = cacheService;
        _notifications = notifications;
        _logger        = logger;
        _instanceInfo  = instanceInfo;
    }

    public IActionResult Index()
    {
        var recent = _notifications.GetRecent(1000);
        var vm = new DashboardViewModel
        {
            Entries              = _cacheService.GetAllEntries().ToList(),
            Stats                = _cacheService.GetStats(),
            KeyRegistry          = _cacheService.GetKeyRegistry().ToList(),
            TagRegistry          = _cacheService.GetTagRegistry().ToList(),
            TotalNotifications   = recent.Count,
            UnreadNotifications  = _notifications.GetUnreadCount(),
            Instance             = _instanceInfo
        };

        _logger.LogDebug(
            "Dashboard loaded | ActiveEntries: {Active} | Hits: {Hits} | Misses: {Misses} | HitRatio: {Ratio}%",
            vm.Stats.ActiveEntries, vm.Stats.Hits, vm.Stats.Misses, vm.Stats.HitRatio);

        return View(vm);
    }

    [HttpGet]
    public IActionResult EntryDetail(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return NotFound();
        var entries = _cacheService.GetAllEntries();
        var meta = entries.FirstOrDefault(e => e.Key == key);
        var keyReg = _cacheService.GetKeyRegistry().FirstOrDefault(k => k.Key == key);
        if (meta is null && keyReg is null)
            return NotFound();
        return Json(new {
            key,
            value        = meta?.Value,
            tags         = meta?.Tags ?? new List<string>(),
            createdAt    = meta?.CreatedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"),
            expiresAt    = meta?.ExpiresAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"),
            expiresInMs  = meta is null ? 0 : (long)(meta.ExpiresAt - DateTimeOffset.UtcNow).TotalMilliseconds,
            isExpired    = meta?.IsExpired ?? true,
            factoryGenerated = meta?.FactoryGenerated ?? false,
            factoryLabel = meta?.FactoryLabel,
            lastAccessedAt = meta?.LastAccessedAt?.ToLocalTime().ToString("HH:mm:ss"),
            hits         = keyReg?.Hits ?? 0,
            misses       = keyReg?.Misses ?? 0,
            hitRatio     = keyReg?.HitRatio ?? 0,
            timesSet     = keyReg?.TimesSet ?? 0,
            firstSeen    = keyReg?.FirstSeen.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"),
            isActive     = keyReg?.IsCurrentlyActive ?? false
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> QuickRemove(string key)
    {
        if (!string.IsNullOrWhiteSpace(key))
        {
            _logger.LogInformation("Quick remove | Key: {Key}", key);
            await _cacheService.RemoveAsync(key);
        }

        TempData["Message"] = $"Entry '{key}' removed.";
        TempData["MessageType"] = "success";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> BulkRemove([FromForm] string keys)
    {
        if (string.IsNullOrWhiteSpace(keys))
        {
            TempData["Message"] = "No keys selected.";
            TempData["MessageType"] = "warning";
            return RedirectToAction(nameof(Index));
        }
        var keyList = keys.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var key in keyList)
            await _cacheService.RemoveAsync(key);
        _logger.LogInformation("Bulk remove | Count: {Count} | Keys: [{Keys}]", keyList.Length, string.Join(", ", keyList));
        TempData["Message"] = $"Removed {keyList.Length} entries.";
        TempData["MessageType"] = "success";
        return RedirectToAction(nameof(Index));
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error() =>
        View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
}
