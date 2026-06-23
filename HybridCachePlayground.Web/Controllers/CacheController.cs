using HybridCachePlayground.Web.Models;
using HybridCachePlayground.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace HybridCachePlayground.Web.Controllers;

public class CacheController : Controller
{
    private readonly ICachePlaygroundService _cacheService;
    private readonly ILogger<CacheController> _logger;
    private readonly int _stampedeMax;

    public CacheController(
        ICachePlaygroundService cacheService,
        IConfiguration config,
        ILogger<CacheController> logger)
    {
        _cacheService = cacheService;
        _logger = logger;
        _stampedeMax = config.GetValue("HybridCache:StampedeMaxConcurrency", 200);
    }

    // ─── ViewData helpers ─────────────────────────────────────────────────────

    private void InjectRecentKeys() =>
        ViewData["RecentKeys"] = _cacheService.GetKeyRegistry()
            .OrderByDescending(k => k.LastSeen)
            .Take(15)
            .Select(k => k.Key)
            .ToList();

    private void InjectAllTags() =>
        ViewData["AllTags"] = _cacheService.GetTagRegistry()
            .OrderByDescending(t => t.TimesUsed)
            .Select(t => t.Tag)
            .ToList();

    // ─── Bulk Set ────────────────────────────────────────────────────────────

    [HttpGet]
    public IActionResult BulkSet()
    {
        InjectAllTags();
        return View(new BulkSetRequest());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> BulkSet(BulkSetRequest model)
    {
        if (!ModelState.IsValid)
        {
            InjectAllTags();
            return View(model);
        }

        _logger.LogInformation(
            "Bulk Add requested | Prefix: {KeyPrefix} | Count: {Count} | TTL: {Ttl}m",
            model.KeyPrefix, model.Count, model.ExpirationMinutes);

        var result = await _cacheService.BulkSetAsync(
            model.KeyPrefix, model.Count, model.ParsedTags, model.ExpirationMinutes);

        ViewData["Result"] = result;
        InjectAllTags();
        return View(model);
    }

    // ─── Set ─────────────────────────────────────────────────────────────────

    [HttpGet]
    public IActionResult Set()
    {
        InjectRecentKeys();
        InjectAllTags();
        return View(new CacheSetRequest());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Set(CacheSetRequest model)
    {
        if (!ModelState.IsValid)
        {
            InjectRecentKeys();
            InjectAllTags();
            return View(model);
        }

        _logger.LogInformation(
            "Set requested | Key: {Key} | Tags: [{Tags}] | TTL: {Ttl}m | Flags: {Flags}",
            model.Key, string.Join(", ", model.ParsedTags), model.ExpirationMinutes ?? 5, model.GetEntryFlags());

        await _cacheService.SetAsync(
            model.Key, model.Value, model.ParsedTags, model.ExpirationMinutes ?? 5, model.GetEntryFlags());

        TempData["Message"] = $"Entry '{model.Key}' stored successfully with {model.ParsedTags.Count} tag(s).";
        TempData["MessageType"] = "success";
        return RedirectToAction(nameof(Set));
    }

    // ─── Get ─────────────────────────────────────────────────────────────────

    [HttpGet]
    public IActionResult Get()
    {
        InjectRecentKeys();
        return View(new CacheGetRequest());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Get(CacheGetRequest model)
    {
        if (!ModelState.IsValid)
        {
            InjectRecentKeys();
            return View(model);
        }

        _logger.LogInformation(
            "Get requested | Key: {Key} | Flags: {Flags} | Template: {Template}",
            model.Key, model.GetEntryFlags(), model.FactoryTemplateIndex);

        var result = await _cacheService.GetOrCreateAsync(
            model.Key,
            model.GetEntryFlags(),
            model.FactoryTemplateIndex,
            model.ParsedTags.Count > 0 ? model.ParsedTags : null,
            string.IsNullOrWhiteSpace(model.FactoryValue) ? null : model.FactoryValue);
        ViewData["Result"] = result;
        InjectRecentKeys();
        return View(model);
    }

    // ─── Remove by key (Single & Bulk) ───────────────────────────────────────

    [HttpGet]
    public IActionResult Remove()
    {
        InjectRecentKeys();
        return View(new CacheRemoveRequest());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Remove(CacheRemoveRequest model)
    {
        if (!ModelState.IsValid)
        {
            InjectRecentKeys();
            return View(model);
        }

        _logger.LogInformation("Remove by key requested | Key: {Key}", model.Key);

        await _cacheService.RemoveAsync(model.Key);

        TempData["Message"] = $"Entry '{model.Key}' removed from cache.";
        TempData["MessageType"] = "success";
        return RedirectToAction(nameof(Remove));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> BulkRemove(BulkRemoveRequest model)
    {
        if (!ModelState.IsValid)
        {
            InjectRecentKeys();
            return RedirectToAction(nameof(Remove));
        }

        var keys = model.ParsedKeys;
        if (keys.Count == 0)
        {
            TempData["Message"] = "At least one key is required";
            TempData["MessageType"] = "danger";
            return RedirectToAction(nameof(Remove));
        }

        _logger.LogInformation("Bulk remove by keys requested | Count: {Count}", keys.Count);

        await _cacheService.RemoveAsync(keys);

        TempData["Message"] = $"{keys.Count} entries removed from cache.";
        TempData["MessageType"] = "success";
        return RedirectToAction(nameof(Remove));
    }

    // ─── Remove by tag (Single & Bulk) ───────────────────────────────────────

    [HttpGet]
    public IActionResult RemoveByTag()
    {
        InjectAllTags();
        return View(new CacheRemoveByTagRequest());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveByTag(CacheRemoveByTagRequest model)
    {
        if (!ModelState.IsValid)
        {
            InjectAllTags();
            return View(model);
        }

        _logger.LogInformation("Remove by tag requested | Tag: {Tag}", model.Tag);

        await _cacheService.RemoveByTagAsync(model.Tag);

        TempData["Message"] = $"Tag '{model.Tag}' is marked as invalid, all associated cache items are now stale.";
        TempData["MessageType"] = "warning";
        return RedirectToAction(nameof(RemoveByTag));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> BulkRemoveByTag(BulkRemoveByTagRequest model)
    {
        if (!ModelState.IsValid)
        {
            TempData["Message"] = "Invalid input";
            TempData["MessageType"] = "danger";
            return RedirectToAction(nameof(RemoveByTag));
        }

        var tags = model.ParsedTags;
        if (tags.Count == 0)
        {
            TempData["Message"] = "At least one tag is required";
            TempData["MessageType"] = "danger";
            return RedirectToAction(nameof(RemoveByTag));
        }

        _logger.LogInformation("Bulk remove by tags requested | Count: {Count}", tags.Count);

        await _cacheService.RemoveByTagAsync(tags);

        TempData["Message"] = $"{tags.Count} tags marked as invalid, all associated cache items are now stale.";
        TempData["MessageType"] = "warning";
        return RedirectToAction(nameof(RemoveByTag));
    }

    // ─── Remove by tag wildcard ───────────────────────────────────────────────

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveByTagWildcard(string wildcardPattern)
    {
        _logger.LogInformation("Remove by wildcard requested | {Pattern}", wildcardPattern);

        await _cacheService.RemoveByTagWildcardAsync(wildcardPattern);

        TempData["Message"] = $"WildCard tag activated. All previous entities invalidated";
        TempData["MessageType"] = "warning";

        return RedirectToAction(nameof(RemoveByTag));
    }

    // ─── Stampede test ───────────────────────────────────────────────────────

    [HttpGet]
    public IActionResult Stampede()
    {
        ViewData["StampedeMax"] = _stampedeMax;
        InjectRecentKeys();
        return View(new StampedeRequest());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Stampede(StampedeRequest model)
    {
        ViewData["StampedeMax"] = _stampedeMax;
        InjectRecentKeys();

        if (model.Concurrency > _stampedeMax)
            ModelState.AddModelError(nameof(model.Concurrency),
                $"Max is {_stampedeMax} — change HybridCache:StampedeMaxConcurrency in appsettings to raise it.");

        if (!ModelState.IsValid)
            return View(model);

        _logger.LogInformation(
            "Stampede test requested | Key: {Key} | Concurrency: {Concurrency} | ForceEvict: {ForceEvict}",
            model.Key, model.Concurrency, model.ForceEvict);

        var result = await _cacheService.RunStampedeTestAsync(model.Key, model.Concurrency, model.ForceEvict);
        ViewData["Result"] = result;
        return View(model);
    }

    // ─── Concurrent GET test ─────────────────────────────────────────────────

    [HttpGet]
    public IActionResult ConcurrentGet()
    {
        InjectRecentKeys();
        ViewData["StampedeMax"] = _stampedeMax;
        return View(new ConcurrentGetRequest());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ConcurrentGet(ConcurrentGetRequest model)
    {
        ViewData["StampedeMax"] = _stampedeMax;
        InjectRecentKeys();
        if (model.Concurrency > _stampedeMax)
            ModelState.AddModelError(nameof(model.Concurrency), $"Max is {_stampedeMax}.");
        if (!ModelState.IsValid) return View(model);
        var result = await _cacheService.RunConcurrentGetTestAsync(model.Key, model.Concurrency, model.FactoryDelayMs, model.ForceEvict);
        ViewData["Result"] = result;
        return View(model);
    }

    // ─── Generate Value (AJAX) ────────────────────────────────────────────────

    [HttpGet]
    public IActionResult GenerateValue(int templateIndex = -1)
    {
        var (label, json, tags) = templateIndex >= 0
            ? RandomDataFactory.GenerateFromTemplate(templateIndex)
            : RandomDataFactory.Generate();

        _logger.LogDebug("GenerateValue called | Template: {Template} | Label: {Label}", templateIndex, label);
        return Json(new { label, json, tags, templateIndex });
    }

    // ─── Template names (AJAX) ────────────────────────────────────────────────

    [HttpGet]
    public IActionResult GetTemplates()
    {
        var templates = RandomDataFactory.Templates
            .Select((t, i) => new { index = i, label = t.Label })
            .ToList();
        return Json(templates);
    }
}
