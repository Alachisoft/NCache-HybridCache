using System.ComponentModel.DataAnnotations;

namespace HybridCachePlayground.Web.Models;

public class BulkSetRequest
{
    [Required(ErrorMessage = "Key prefix is required")]
    [StringLength(100, MinimumLength = 1)]
    public string KeyPrefix { get; set; } = "bulk";

    [Range(1, 500, ErrorMessage = "Count must be between 1 and 500")]
    public int Count { get; set; } = 10;

    /// <summary>Comma-separated tags to attach to every generated entry.</summary>
    public string? Tags { get; set; }

    [Range(1, 1440, ErrorMessage = "Expiration must be between 1 and 1440 minutes")]
    public int ExpirationMinutes { get; set; } = 5;

    public List<string> ParsedTags =>
        string.IsNullOrWhiteSpace(Tags)
            ? []
            : Tags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
}
