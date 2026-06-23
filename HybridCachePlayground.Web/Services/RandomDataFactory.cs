using System.Text.Json;

namespace HybridCachePlayground.Web.Services;

public static class RandomDataFactory
{
    private static readonly Random _rng = Random.Shared;

    // ─── Value pools ─────────────────────────────────────────────────────────
    private static readonly string[] _firstNames = ["Alice", "Bob", "Charlie", "Diana", "Ethan", "Fiona", "George", "Hannah", "Ivan", "Julia"];
    private static readonly string[] _lastNames  = ["Smith", "Johnson", "Williams", "Brown", "Jones", "Garcia", "Miller", "Davis", "Wilson", "Moore"];
    private static readonly string[] _roles       = ["Admin", "Editor", "Viewer", "Manager", "Developer", "Analyst", "Designer", "Architect"];
    private static readonly string[] _departments = ["Engineering", "Marketing", "Sales", "HR", "Finance", "Operations", "Support", "Legal"];
    private static readonly string[] _statuses    = ["active", "inactive", "pending", "suspended", "verified", "archived"];
    private static readonly string[] _categories  = ["Electronics", "Clothing", "Food", "Books", "Sports", "Toys", "Home", "Automotive"];
    private static readonly string[] _cities      = ["New York", "London", "Tokyo", "Sydney", "Berlin", "Paris", "Toronto", "Dubai"];
    private static readonly string[] _countries   = ["US", "UK", "JP", "AU", "DE", "FR", "CA", "AE"];
    private static readonly string[] _actions     = ["page_view", "click", "purchase", "signup", "logout", "search", "download", "share"];
    private static readonly string[] _tiers       = ["Free", "Basic", "Pro", "Enterprise"];
    private static readonly string[] _priorities  = ["low", "medium", "high", "critical"];

    // ─── Templates: (label, ordered field list) ───────────────────────────────
    // 3–5 fields are randomly sampled from each template's list per call.
    public static readonly (string Label, string[] Fields, string[] Tags)[] Templates =
    [
        ("User",      ["id", "firstName", "lastName", "email", "role", "department", "status"],   ["user", "identity", "user-data"]),
        ("Product",   ["id", "name", "price", "category", "stock", "status", "tier"],             ["product", "catalog", "inventory"]),
        ("Order",     ["orderId", "customerId", "total", "status", "createdAt", "itemCount", "priority"], ["order", "transaction", "orders"]),
        ("Session",   ["sessionId", "userId", "ipAddress", "expiresAt", "role", "country"],       ["session", "auth", "session-data"]),
        ("Analytics", ["eventId", "userId", "action", "city", "country", "timestamp"],            ["analytics", "events", "tracking"]),
        ("Employee",  ["id", "firstName", "lastName", "department", "salary", "createdAt", "status"], ["employee", "hr", "staff"]),
    ];

    /// <summary>Generates a random JSON object. Returns (templateLabel, prettyJson, suggestedTags).</summary>
    public static (string Label, string Json, string[] Tags) Generate()
    {
        var (label, fields, tags) = Templates[_rng.Next(Templates.Length)];
        var count = _rng.Next(3, 6); // 3, 4, or 5 fields
        var selected = fields.OrderBy(_ => _rng.Next()).Take(count);

        var dict = new Dictionary<string, object?>();
        foreach (var field in selected)
            dict[field] = GenerateValue(field);

        var json = JsonSerializer.Serialize(dict, new JsonSerializerOptions { WriteIndented = true });
        return (label, json, tags);
    }

    /// <summary>Generates a specific template by index (0-based). Useful for seeding.</summary>
    public static (string Label, string Json, string[] Tags) GenerateFromTemplate(int templateIndex)
    {
        templateIndex = Math.Clamp(templateIndex, 0, Templates.Length - 1);
        var (label, fields, tags) = Templates[templateIndex];
        var count = _rng.Next(3, 6);
        var selected = fields.OrderBy(_ => _rng.Next()).Take(count);

        var dict = new Dictionary<string, object?>();
        foreach (var field in selected)
            dict[field] = GenerateValue(field);

        var json = JsonSerializer.Serialize(dict, new JsonSerializerOptions { WriteIndented = true });
        return (label, json, tags);
    }

    private static object? GenerateValue(string field) => field switch
    {
        "id" or "customerId" or "userId"
            => _rng.Next(1000, 9999),
        "sessionId" or "eventId" or "orderId"
            => Guid.NewGuid().ToString()[..8].ToUpper(),
        "firstName"
            => _firstNames[_rng.Next(_firstNames.Length)],
        "lastName"
            => _lastNames[_rng.Next(_lastNames.Length)],
        "name"
            => $"{_firstNames[_rng.Next(_firstNames.Length)]} {_lastNames[_rng.Next(_lastNames.Length)]}",
        "email"
            => $"{_firstNames[_rng.Next(_firstNames.Length)].ToLower()}.{_lastNames[_rng.Next(_lastNames.Length)].ToLower()}@example.com",
        "role"     => _roles[_rng.Next(_roles.Length)],
        "department" => _departments[_rng.Next(_departments.Length)],
        "status"   => _statuses[_rng.Next(_statuses.Length)],
        "category" => _categories[_rng.Next(_categories.Length)],
        "city"     => _cities[_rng.Next(_cities.Length)],
        "country"  => _countries[_rng.Next(_countries.Length)],
        "action"   => _actions[_rng.Next(_actions.Length)],
        "tier"     => _tiers[_rng.Next(_tiers.Length)],
        "priority" => _priorities[_rng.Next(_priorities.Length)],
        "price"    => Math.Round(_rng.NextDouble() * 499 + 1, 2),
        "salary"   => _rng.Next(40, 150) * 1000,
        "total"    => Math.Round(_rng.NextDouble() * 999 + 10, 2),
        "stock"    => _rng.Next(0, 500),
        "itemCount" => _rng.Next(1, 10),
        "ipAddress" => $"{_rng.Next(1, 255)}.{_rng.Next(0, 255)}.{_rng.Next(0, 255)}.{_rng.Next(1, 255)}",
        "createdAt" or "timestamp"
            => DateTimeOffset.UtcNow.AddDays(-_rng.Next(0, 365)).ToString("yyyy-MM-ddTHH:mm:ssZ"),
        "expiresAt"
            => DateTimeOffset.UtcNow.AddHours(_rng.Next(1, 48)).ToString("yyyy-MM-ddTHH:mm:ssZ"),
        _ => null
    };
}
