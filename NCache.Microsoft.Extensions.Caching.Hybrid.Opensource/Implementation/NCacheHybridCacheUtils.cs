using Alachisoft.NCache.Client;

namespace NCache.OSS.Microsoft.Extensions.Caching.Hybrid
{
    /// <summary>
    /// Provides default values and utility methods for the NCache hybrid cache implementation.
    /// </summary>
    /// <remarks>This static class contains constants and helper methods used internally by the NCache hybrid
    /// cache system, such as configuration section names, wildcard values, and tag validation logic. It is intended for
    /// internal use and is not designed for direct consumption by application code.</remarks>
    internal static class NCacheHybridCacheUtils
    {
        /// <summary>
        /// Generates a unique identifier string for the specified cache instance and configuration.
        /// </summary>
        /// <param name="cache">The cache instance for which to generate the identifier. Must not be null.</param>
        /// <param name="config">The cache configuration containing the distributed cache name. Must not be null.</param>
        /// <param name="uniqueId">Unique process ID to differentiate message recipient and sender.</param>
        /// <returns>A string representing the unique identifier, composed of the cache client's IP address and the distributed
        /// cache name.</returns>
        internal static string GetIdentifier(ICache cache, NCacheHybridCacheOptions config, Guid uniqueId)
    => $"{Environment.MachineName}:{Environment.ProcessId}:{cache.ClientInfo.IPAddress}:{config.DistributedCacheName}:{uniqueId:N}";

        /// <summary>
        /// Generates the topic name used for notifications based on the specified distributed cache name.
        /// </summary>
        /// <param name="distributedCacheName">The name of the distributed cache for which to generate the topic name. Cannot be null.</param>
        /// <returns>A string representing the topic name associated with the specified distributed cache.</returns>
        internal static string GetTopicName(NCacheHybridCacheOptions config) => config.DistributedCacheName + "_NHCT_TOPIC";

        /// <summary>
        /// Represents the prefix used to identify sentinel values within the application.
        /// </summary>
        private const string SentinelPrefix = "__NHCT__";

        /// <summary>
        /// Represents the suffix used to identify sentinel values within the application.
        /// </summary>
        private const string SentinelSuffix = "__";

        /// <summary>
        /// Represents the wildcard character used for pattern matching or to indicate any value.
        /// </summary>
        internal const string Wildcard = "*";

        /// <summary>
        /// Specifies the configuration section name for the NCache hybrid cache settings.
        /// </summary>
        internal const string ConfigSectionName = "NCacheHybridCache";

        /// <summary>
        /// Method to generate a sentinel key by combining the predefined prefix and suffix with the provided tag. Sentinel keys are used internally to represent tags in the cache system, allowing for efficient invalidation and grouping of cache entries based on tags.
        /// </summary>
        /// <param name="tag">The tag for which to generate a sentinel key.</param>
        /// <returns>The generated sentinel key</returns>
        internal static string ToSentinelKey(string tag) => SentinelPrefix + tag + SentinelSuffix;

        /// <summary>
        /// Extracts the original key from a sentinel key by removing the sentinel prefix and suffix.
        /// </summary>
        /// <param name="sentinelKey">The sentinel key string from which to extract the original key. Must include both the sentinel prefix and
        /// suffix.</param>
        /// <returns>A string containing the original key extracted from the specified sentinel key.</returns>
        internal static string FromSentinelKey(string sentinelKey) => sentinelKey.Substring(SentinelPrefix.Length, sentinelKey.Length - SentinelPrefix.Length - SentinelSuffix.Length);

        /// <summary>
        /// Validates that the specified tag name is not null, empty, whitespace, or a reserved wildcard value.
        /// </summary>
        /// <param name="tag">The tag name to validate. Cannot be null, empty, consist only of whitespace, or be the reserved wildcard
        /// '*'.</param>
        /// <exception cref="ArgumentException">Thrown if the tag is null, empty, consists only of whitespace, or is the reserved wildcard '*'.</exception>
        internal static void ValidateTagName(string tag, bool isMarkedInvalid = false)
        {
            // Check if the tag is null, empty, or consists only of whitespace characters
            if (string.IsNullOrWhiteSpace(tag))
                // If the tag is invalid, throw an ArgumentException with a descriptive message and the parameter name
                throw new ArgumentException("Tag cannot be null, empty or whitespace.", nameof(tag));

            // Check if the tag is the reserved wildcard value
            if (!isMarkedInvalid && tag == NCacheHybridCacheUtils.Wildcard)
                // If the tag is the reserved wildcard, throw an ArgumentException indicating that this value is not allowed for individual tags
                throw new ArgumentException("The wildcard '*' is reserved for master clock invalidation.", nameof(tag));
        }

        /// <summary>
        /// Validates that the specified tag name is not null, empty, whitespace, or a reserved wildcard value.
        /// </summary>
        /// <param name="tag">The tag name to validate. Cannot be null, empty, consist only of whitespace, or be the reserved wildcard
        /// '*'.</param>
        /// <exception cref="ArgumentException">Thrown if the tag is null, empty, consists only of whitespace, or is the reserved wildcard '*'.</exception>
        internal static void ValidateTagNames(IEnumerable<string> tags)
        {
            // Check if the collection of tags is null or empty
            if (tags == null || !tags.Any()) throw new ArgumentNullException(nameof(tags));

            // Iterate through each tag in the provided collection
            foreach (var tag in tags)
            {
                // Validate the current tag
                ValidateTagName(tag);
            }
        }

        /// <summary>
        /// Asynchronously retrieves the current Unix timestamp in milliseconds.
        /// </summary>
        /// <returns>A task that represents the asynchronous operation. The task result contains the current Unix timestamp,
        /// expressed as the number of milliseconds that have elapsed since 00:00:00 UTC on 1 January 1970.</returns>
        internal static Task<long> GetCurrentUnixTimestampAsync() => Task.FromResult(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
    }
}