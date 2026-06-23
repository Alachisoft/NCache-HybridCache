using System.ComponentModel.DataAnnotations;
namespace HybridCachePlayground.Web.Models;
public class ConcurrentGetRequest
{
    [Required]
    public string Key { get; set; } = string.Empty;
    [Range(2, 500)]
    public int Concurrency { get; set; } = 20;
    [Range(0, 5000)]
    public int FactoryDelayMs { get; set; } = 50;
    public bool ForceEvict { get; set; }
}
