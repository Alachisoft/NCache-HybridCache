namespace HybridCachePlayground.Web.Models;

public class TagRegistryEntry
{
    public string Tag { get; set; } = string.Empty;
    public int TimesUsed { get; set; }
    public int ActiveEntries { get; set; }
    public DateTimeOffset FirstSeen { get; set; }
    public DateTimeOffset LastSeen { get; set; }
    public List<string> KnownKeys { get; set; } = [];
}
