namespace HybridCachePlayground.Web.Models;

public class CacheGetResult
{
    public string Key { get; set; } = string.Empty;
    public string? Value { get; set; }
    public bool IsHit { get; set; }
    public bool HasResult { get; set; }
    public string? FactoryLabel { get; set; }
}
