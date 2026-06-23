namespace HybridCachePlayground.Web.Models;
public record InstanceInfo(
    string Id,
    string Color,
    string MachineName,
    DateTimeOffset StartedAt);
