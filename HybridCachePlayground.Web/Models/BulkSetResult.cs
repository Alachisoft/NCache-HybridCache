namespace HybridCachePlayground.Web.Models;

public class BulkSetResult
{
    public string KeyPrefix { get; set; } = string.Empty;
    public int Requested { get; set; }
    public int Added { get; set; }
    public long ElapsedMs { get; set; }
    public List<BulkSetResultEntry> Entries { get; set; } = [];
}

public class BulkSetResultEntry
{
    public string Key { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string ValuePreview { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = [];
}
