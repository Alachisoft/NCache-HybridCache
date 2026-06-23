using System.ComponentModel.DataAnnotations;

namespace HybridCachePlayground.Web.Models;

public class CacheRemoveRequest
{
    [Required(ErrorMessage = "Key is required")]
    public string Key { get; set; } = string.Empty;
}

public class CacheRemoveByTagRequest
{
    [Required(ErrorMessage = "Tag is required")]
    public string Tag { get; set; } = string.Empty;
}
