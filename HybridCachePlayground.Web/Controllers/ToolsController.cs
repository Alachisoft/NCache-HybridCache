using HybridCachePlayground.Web.Models;
using HybridCachePlayground.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace HybridCachePlayground.Web.Controllers;

public class ToolsController : Controller
{
    private readonly IDebugToolsService      _debug;
    private readonly INotificationService    _notifications;
    private readonly LogFilePathProvider     _logPath;
    private readonly ILogger<ToolsController> _logger;

    public ToolsController(
        IDebugToolsService debug,
        INotificationService notifications,
        LogFilePathProvider logPath,
        ILogger<ToolsController> logger)
    {
        _debug         = debug;
        _notifications = notifications;
        _logPath       = logPath;
        _logger        = logger;
    }

    // ─── Debug / Tools page ───────────────────────────────────────────────────

    [HttpGet]
    public IActionResult Index()
    {
        _logger.LogDebug("Tools page loaded");
        return View();
    }

    // ─── Clear L1 ────────────────────────────────────────────────────────────

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ClearL1()
    {
        _logger.LogInformation("Clear L1 Cache requested via Tools page");
        await _debug.ClearL1CacheAsync();
        TempData["Message"]     = "Cleared L1 Local Cache";
        TempData["MessageType"] = "Success";
        return RedirectToAction(nameof(Index));
    }

    // ─── Clear L2 ────────────────────────────────────────────────────────────

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ClearL2()
    {
        _logger.LogInformation("Clear L2 Cache requested via Tools page");
        await _debug.ClearL2CacheAsync();
        TempData["Message"] = "Cleared L2 Distributed Cache";
        TempData["MessageType"] = "Success";
        return RedirectToAction(nameof(Index));
    }

    // ─── Reset Statistics ─────────────────────────────────────────────────────

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetStatistics()
    {
        await _debug.ResetStatisticsAsync();
        TempData["Message"]     = "Statistics reset — hit/miss/factory counters zeroed.";
        TempData["MessageType"] = "success";
        return RedirectToAction(nameof(Index));
    }

    // ─── Prune Expired ───────────────────────────────────────────────────────

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult PruneExpired()
    {
        _debug.PruneExpiredEntries();
        TempData["Message"]     = "Expired metadata entries pruned.";
        TempData["MessageType"] = "success";
        return RedirectToAction(nameof(Index));
    }

    // ─── Simulate Pressure ───────────────────────────────────────────────────

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SimulatePressure(string keyPrefix, int entryCount)
    {
        if (string.IsNullOrWhiteSpace(keyPrefix)) keyPrefix = "pressure";
        entryCount = Math.Clamp(entryCount, 1, 500);

        _logger.LogInformation(
            "Simulate Pressure | Prefix: {Prefix} | Count: {Count}", keyPrefix, entryCount);

        var added = await _debug.SimulatePressureAsync(keyPrefix, entryCount);
        TempData["Message"]     = $"Pressure simulation complete — {added} entries added with prefix '{keyPrefix}'.";
        TempData["MessageType"] = "warning";
        return RedirectToAction(nameof(Index));
    }

    // ─── Export Snapshot ─────────────────────────────────────────────────────

    [HttpGet]
    public async Task<IActionResult> ExportSnapshot()
    {
        _logger.LogInformation("Export snapshot requested");
        var json = await _debug.ExportSnapshotAsync();
        return File(
            System.Text.Encoding.UTF8.GetBytes(json),
            "application/json",
            $"cache-snapshot-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}.json");
    }

    // ─── Log cache state ─────────────────────────────────────────────────────

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult LogState()
    {
        _debug.LogCacheState();
        TempData["Message"]     = "Cache state written to log.";
        TempData["MessageType"] = "success";
        return RedirectToAction(nameof(Index));
    }

    // ─── Log viewer page ──────────────────────────────────────────────────────

    [HttpGet]
    public IActionResult Logs()
    {
        ViewData["LogFilePath"] = _logPath.CurrentLogPath;
        return View();
    }

    // ─── Log content (AJAX polling) ───────────────────────────────────────────

    [HttpGet]
    public IActionResult LogContent(int skip = 0)
    {
        var path = _logPath.CurrentLogPath;

        if (!System.IO.File.Exists(path))
            return Json(new { lines = Array.Empty<string>(), total = 0 });

        try
        {
            // Read with FileShare.ReadWrite so Serilog can still write
            using var fs     = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new StreamReader(fs);
            var allLines     = new List<string>();
            string? line;
            while ((line = reader.ReadLine()) != null)
                allLines.Add(line);

            var page = allLines.Skip(skip).ToList();
            return Json(new { lines = page, total = allLines.Count });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not read log file {Path}", path);
            return Json(new { lines = Array.Empty<string>(), total = 0, error = ex.Message });
        }
    }

    // ─── Notifications (AJAX) ────────────────────────────────────────────────

    [HttpGet]
    public IActionResult NotificationsData(int count = 20)
    {
        var items = _notifications.GetRecent(count);
        return Json(new
        {
            unread  = _notifications.GetUnreadCount(),
            entries = items.Select(n => new
            {
                id        = n.Id,
                title     = n.Title,
                message   = n.Message,
                level     = n.Level.ToString(),
                badgeClass = n.BadgeClass,
                timestamp = n.Timestamp.ToString("HH:mm:ss"),
                isRead    = n.IsRead
            })
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult MarkNotificationsRead()
    {
        _notifications.MarkAllRead();
        return Ok();
    }
}
