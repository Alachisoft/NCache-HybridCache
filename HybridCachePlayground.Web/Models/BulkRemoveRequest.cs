using System.ComponentModel.DataAnnotations;

namespace HybridCachePlayground.Web.Models;

public class BulkRemoveRequest
{
    [Required(ErrorMessage = "Keys are required")]
    public string Keys { get; set; } = string.Empty;

    /// <summary>
    /// Parsed keys from the comma/newline-separated input
    /// </summary>
    public List<string> ParsedKeys
    {
        get
        {
            if (string.IsNullOrWhiteSpace(Keys)) return new List<string>();
            return Keys.Split(new[] { ',', '\n', '\r', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                       .Where(k => !string.IsNullOrWhiteSpace(k))
                       .Distinct()
                       .ToList();
        }
    }
}

public class BulkRemoveByTagRequest
{
    [Required(ErrorMessage = "Tags are required")]
    public string Tags { get; set; } = string.Empty;

    /// <summary>
    /// Parsed tags from the comma/newline-separated input
    /// </summary>
    public List<string> ParsedTags
    {
        get
        {
            if (string.IsNullOrWhiteSpace(Tags)) return new List<string>();
            return Tags.Split(new[] { ',', '\n', '\r', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                       .Where(t => !string.IsNullOrWhiteSpace(t))
                       .Distinct()
                       .ToList();
        }
    }
}
