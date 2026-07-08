using Alachisoft.NCache.Client;
using Alachisoft.NCache.Runtime.Caching;
using System.Collections.Concurrent;

namespace NCache.OSS.Caching.Hybrid
{
    /// <summary>
    /// Records tags that have been marked for logical deletion and keeps a death-time for each tag.
    /// The collection contains only tags that were explicitly marked. It is thread-safe and intended
    /// to be used for invalidation/logical deletion checks.
    /// </summary>
    internal sealed class TagKeeper : IDisposable
    {
        #region Fields

        /// <summary>
        /// Stores the mapping of cache tag names to tasks that provide their latest invalidation timestamps.
        /// </summary>
        /// <remarks>This dictionary enables concurrent access to tag invalidation times, supporting
        /// thread-safe cache invalidation scenarios. Each entry associates a tag with an asynchronous operation that
        /// retrieves the time the tag was last invalidated.</remarks>
        private readonly ConcurrentDictionary<string, Task<long>> _tagInvalidationTimes = new();

        /// <summary>
        /// Represents the distributed cache instance used for storing and retrieving tags and their invalidation timestamps.
        /// </summary>
        private readonly ICache _distributedCache;

        /// <summary>
        /// Represents the expiration time for sentinel cache items that track tag invalidation timestamps in the distributed cache.
        /// </summary>
        /// <remarks>This value is derived from the configured expiration time of distributed cache entries and can be null if no 
        /// expiration is specified in the configuration.</remarks>
        private readonly TimeSpan? _sentinelExpiration;

        /// <summary>
        /// Represents a Master Clock invalidation timestamp that applies to all cache entries regardless of their individual tags
        /// </summary>
        private Task<long> _globalInvalidationTimestamp = Task.FromResult(0L);

        #endregion

        #region Constructor and private methods

        /// <summary>
        /// Initializes a new instance of the TagKeeper class with the specified distributed cache and configuration
        /// settings.
        /// </summary>
        /// <param name="distributedCache">The distributed cache implementation to be used for storing and retrieving cache entries.</param>
        /// <param name="config">The configuration settings that define cache behavior, including default entry options.</param>
        /// <exception cref="ArgumentNullException">Thrown if distributedCache is null.</exception>
        internal TagKeeper(ICache distributedCache, NCacheHybridCacheOptions config)
        {
            _distributedCache = distributedCache ?? throw new ArgumentNullException(nameof(distributedCache));
            _sentinelExpiration = config.DistributedCacheExpiration ?? null;
        }

        /// <summary>
        /// Asynchronously inserts a tag and its associated timestamp into the distributed cache as a sentinel value.
        /// </summary>
        /// <remarks>If a sentinel expiration is configured, the cached item will expire after the
        /// specified duration. The method does not return the inserted value.</remarks>
        /// <param name="tag">The tag to be stored as a sentinel key in the distributed cache. Cannot be null.</param>
        /// <param name="timestamp">A task that represents the timestamp value to associate with the tag. The result of this task is used as the
        /// cached value.</param>
        /// <param name="cancellationToken">A cancellation token that can be used to cancel the asynchronous operation.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        private async Task SendTagToL2Async(string tag, Task<long> timestamp, CancellationToken cancellationToken)
        {
            // Runs the operation of inserting a sentinel value for the specified tag into the distributed cache on a background thread
            await Task.Run<long>(() =>
            {
                // Generate the sentinel key for the tag using a predefined format
                var key = NCacheHybridCacheUtils.ToSentinelKey(tag);

                // Create a new CacheItem with the timestamp result as its value
                var item = new CacheItem(timestamp.Result);

                // Check if a sentinel expiration duration is configured
                if (_sentinelExpiration.HasValue)
                    // If an expiration is configured, set the expiration for the cache item to be absolute with the specified duration
                    item.Expiration = new Expiration(ExpirationType.Absolute, _sentinelExpiration.Value);

                // Insert the cache item into the distributed cache using the generated sentinel key
                _distributedCache.Insert(key, item);

                // Return the timestamp result as the result of the task, although this value is not used by the caller
                return timestamp.Result;
            }, cancellationToken);
        }

        /// <summary>
        /// Invalidates all cache tags by setting a global wildcard invalidation timestamp and clearing individual tag
        /// invalidation times.
        /// </summary>
        /// <remarks>This method clears all tag-based cache invalidations and sets a new global
        /// invalidation point. After calling this method, all previously cached items associated with any tag are
        /// considered invalidated. The operation affects both in-memory and distributed cache layers.</remarks>
        /// <param name="cancellationToken">A cancellation token that can be used to cancel the operation.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains the new global invalidation
        /// timestamp, in milliseconds since the Unix epoch.</returns>
        private async Task<long> SetWildCard(CancellationToken cancellationToken)
        {
            // Retrieve the current timestamp in milliseconds since the Unix epoch to use as the global invalidation time
            return await Task.Run<long>(() =>
            {
                // Update the Master Clock invalidation timestamp to the current time
                _globalInvalidationTimestamp = NCacheHybridCacheUtils.GetCurrentUnixTimestampAsync();

                // Check if there are any individual tag invalidation times currently stored
                if (_tagInvalidationTimes.Any())
                {
                    // Generate a list of sentinel keys for all currently invalidated tags
                    var sentinelKeys = _tagInvalidationTimes.Keys.Select(NCacheHybridCacheUtils.ToSentinelKey).ToArray();

                    // Remove all sentinel keys from the distributed cache to clear individual tag invalidation times
                    _distributedCache.RemoveBulk(sentinelKeys);

                    // Clear the in-memory dictionary of tag invalidation times
                    _tagInvalidationTimes.Clear();
                }

                // Insert a sentinel value for the wildcard tag into the distributed cache
                SendTagToL2Async(NCacheHybridCacheUtils.Wildcard, _globalInvalidationTimestamp, cancellationToken).Wait();

                // Return the global invalidation timestamp as the result of the task
                return _globalInvalidationTimestamp.Result;
            }, cancellationToken);
        }

        /// <summary>
        /// Retrieves the long value associated with the specified tag from the distributed cache asynchronously.
        /// </summary>
        /// <remarks>If the tag does not exist in the cache or the value cannot be retrieved as a long,
        /// the method returns <see cref="long.MinValue"/>.</remarks>
        /// <param name="tag">The tag key used to locate the cached value.</param>
        /// <param name="cancellationToken">A cancellation token that can be used to cancel the asynchronous operation.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains the long value associated with
        /// the tag if found; otherwise, <see cref="long.MinValue"/>.</returns>
        private Task<long> GetTagFromL2(string tag, CancellationToken cancellationToken)
        {
            // Return a task that runs the syncing specified tag from the distributed cache on a background thread
            return Task.Run<long>(() =>
            {
                // Generate the sentinel key for the specified tag using a predefined format
                var key = NCacheHybridCacheUtils.ToSentinelKey(tag);

                // Attempt to retrieve the cache item associated with the generated sentinel key from the distributed cache
                var item = _distributedCache.GetCacheItem(key);

                // Check if the retrieved cache item is not null and its value can be cast to a long
                if (item != null && item.GetValue<long>() is long timestamp)
                    // If true, return the retrieved timestamp as the result of the task
                    return timestamp;

                // If the cache item is null or its value cannot be cast to a long then return long.MinValue
                return long.MinValue;
            }, cancellationToken);
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Synchronizes the specified tag from the L2 cache to the L1 cache asynchronously.
        /// </summary>
        /// <remarks>Use this method to ensure that the L1 cache reflects the latest state of the
        /// specified tag as stored in the L2 cache. If the operation is canceled via the provided token, the
        /// synchronization may not complete.</remarks>
        /// <param name="tag">The name of the tag to synchronize from the L2 cache.</param>
        /// <param name="cancellationToken">A cancellation token that can be used to cancel the operation.</param>
        /// <returns></returns>
        internal async Task SyncL1TagFromL2(IEnumerable<string> tags, CancellationToken cancellationToken)
        {
            // Iterate through each tag in the provided collection
            foreach (var tag in tags)
            {
                // Synchronize the current tag from the L2 cache
                _tagInvalidationTimes[tag] = GetTagFromL2(tag, cancellationToken);
            }
        }

        /// <summary>
        /// Synchronizes the wildcard tag's invalidation timestamp from the L2 cache and updates the master clock
        /// accordingly.
        /// </summary>
        /// <remarks>This method fetches the current invalidation timestamp for the wildcard tag from the
        /// L2 cache and updates the master clock to reflect this value. Use this method to ensure that the master clock
        /// remains consistent with the L2 cache state.</remarks>
        /// <param name="cancellationToken">A cancellation token that can be used to cancel the operation.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains the updated invalidation
        /// timestamp retrieved from the L2 cache.</returns>
        internal async Task<long> SyncWildCardFromL2(CancellationToken cancellationToken)
        {
            // Fetch the Master Clock timestamp for the wildcard tag from the L2 cache and update the Master Clock with this value
            _globalInvalidationTimestamp = GetTagFromL2(NCacheHybridCacheUtils.Wildcard, cancellationToken);

            // Clear all individual tag invalidation times from the in-memory dictionary since the wildcard invalidation supersedes them
            _tagInvalidationTimes.Clear();

            // Return the updated Master Clock invalidation timestamp as the result of the task
            return await _globalInvalidationTimestamp;
        }

        /// <summary>
        /// Marks the specified tag as invalid and updates its invalidation timestamp asynchronously.
        /// </summary>
        /// <remarks>If the specified tag is a wildcard, all tags are considered invalidated. The method
        /// updates the invalidation time for the tag and stores a sentinel value in the distributed cache. This
        /// operation is thread-safe.</remarks>
        /// <param name="tag">The name of the tag to mark as invalid. Cannot be null or empty.</param>
        /// <param name="cancellationToken">A cancellation token that can be used to cancel the asynchronous operation.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains the Unix timestamp, in
        /// milliseconds, when the tag was marked as invalid.</returns>
        internal Task<long> MarkTagInvalidAsync(string tag, CancellationToken cancellationToken)
        {
            // Validate the tag name to ensure it is not null, empty, whitespace, or a reserved wildcard value
            NCacheHybridCacheUtils.ValidateTagName(tag, true);

            // Check if the tag is the reserved wildcard
            if (tag == NCacheHybridCacheUtils.Wildcard)
            {
                // If true, set the Master Clock and run the wildcard invalidation logic
                return SetWildCard(cancellationToken);
            }
            else
            {
                // If the tag is not a wildcard, mark it as invalid by updating its invalidation timestamp and storing a sentinel value in the distributed cache
                return Task.Run<long>(() =>
                {
                    // Retrieve the current timestamp in milliseconds since the Unix epoch to use as the invalidation time for the tag
                    var task = NCacheHybridCacheUtils.GetCurrentUnixTimestampAsync();

                    // Update the in-memory dictionary with the new invalidation timestamp for the tag
                    _tagInvalidationTimes[tag] = task;

                    // Update the distributed cache with a sentinel value for the tag to indicate its invalidation time
                    SendTagToL2Async(tag, task, cancellationToken).Wait();

                    // Return the invalidation timestamp for the tag as the result of the task
                    return task;
                }, cancellationToken);
            }
        }

        /// <summary>
        /// Asynchronously marks multiple tags as invalid and updates their invalidation timestamps.
        /// </summary>
        /// <remarks>This method marks multiple tags as invalid in a single operation, improving
        /// efficiency when invalidating multiple tags at once.</remarks>
        /// <param name="tags">The collection of tag names to mark as invalid. Cannot be null or empty.</param>
        /// <param name="cancellationToken">A cancellation token that can be used to cancel the asynchronous operation.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        internal Task<long> MarkTagInvalidAsync(IEnumerable<string> tags, CancellationToken cancellationToken)
        {
            // Validate the collection of tag names to ensure it is not null, empty, or containing invalid tag names
            NCacheHybridCacheUtils.ValidateTagNames(tags);

            // If the tag is not a wildcard, mark it as invalid by updating its invalidation timestamp and storing a sentinel value in the distributed cache
            return Task.Run<long>(() =>
            {
                // Retrieve the current timestamp in milliseconds since the Unix epoch to use as the invalidation time for the tag
                var task = NCacheHybridCacheUtils.GetCurrentUnixTimestampAsync();

                // Create a dictionary to hold the cache items that will be inserted into the distributed cache for each tag
                var cacheItems = new Dictionary<string, CacheItem>();

                // Iterate through each tag in the provided collection
                foreach (var tag in tags)
                {
                    // Update the in-memory dictionary with the new invalidation timestamp for the tag
                    _tagInvalidationTimes[tag] = task;

                    // Generate the sentinel key for the tag using a predefined format
                    var key = NCacheHybridCacheUtils.ToSentinelKey(tag);

                    // Create a new CacheItem with the timestamp result as its value
                    var item = new CacheItem(task.Result);

                    // Check if a sentinel expiration duration is configured
                    if (_sentinelExpiration.HasValue)
                        // If an expiration is configured, set the expiration for the cache item to be absolute with the specified duration
                        item.Expiration = new Expiration(ExpirationType.Absolute, _sentinelExpiration.Value);

                    // Add the cache item to the dictionary of items to be inserted into the distributed cache
                    cacheItems[key] = item;
                }

                // Insert all cache items for the specified tags into the distributed cache in bulk
                _distributedCache.InsertBulk(cacheItems);

                // Return the invalidation timestamp for the tags as the result of the task
                return task;
            }, cancellationToken);
        }

        /// <summary>
        /// Determines asynchronously whether an item is invalidated based on its creation timestamp and associated
        /// tags.
        /// </summary>
        /// <remarks>An item is considered invalidated if the global invalidation timestamp or any of the
        /// specified tag invalidation times are greater than or equal to the item's creation timestamp. If no tags are
        /// provided, only the global invalidation timestamp is considered.</remarks>
        /// <param name="creationTimestampUnixMs">The creation timestamp of the item, expressed as the number of milliseconds since the Unix epoch. Used to
        /// compare against invalidation times.</param>
        /// <param name="tags">A collection of tags associated with the item. Each tag is checked for individual invalidation times. Can be
        /// null or empty if no tags are associated.</param>
        /// <param name="cancellationToken">A cancellation token that can be used to cancel the asynchronous operation.</param>
        /// <returns>A task that represents the asynchronous operation. The task result is <see langword="true"/> if the item is
        /// considered invalidated; otherwise, <see langword="false"/>.</returns>
        internal async Task<bool> IsItemInvalidatedAsync(long creationTimestampUnixMs, IEnumerable<string> tags, CancellationToken cancellationToken)
        {
            // Check if the Master Clock has 0L value this means there has been no sync with L2
            if (_globalInvalidationTimestamp.Result == 0L)
                // If the Master Clock has not synced from L2, we need to sync this value with L2
                await SyncWildCardFromL2(cancellationToken).ConfigureAwait(false);

            // Check if the Master Clock invalidation timestamp is greater than or equal to the item's creation timestamp
            if (_globalInvalidationTimestamp.Result >= creationTimestampUnixMs)
                // If true, the item is invalidated due to a global invalidation, so we can return true without checking individual tags
                return true;

            // If there are no tags, we can return false at this point since the global invalidation check has already been performed
            if (tags == null || !tags.Any()) return false;

            // Create a list to hold tags that need to be checked against the distributed cache for their invalidation times
            List<string> searchList = new List<string>();

            // Iterate through each tag associated with the item
            foreach (var tag in tags)
            {
                // Validate the tag name to ensure it is not null, empty, whitespace, or a reserved wildcard value
                NCacheHybridCacheUtils.ValidateTagName(tag);

                // Check if the tag's invalidation time is already known and stored in the in-memory dictionary
                if (_tagInvalidationTimes.TryGetValue(tag, out var existing))
                {
                    // If the existing invalidation time for the tag is greater than or equal to the item's creation timestamp, the item is invalidated
                    if (existing.Result >= creationTimestampUnixMs) return true;
                }
                else
                {
                    // If the tag's invalidation time is not known, add it to the search list to check against the distributed cache
                    searchList.Add(NCacheHybridCacheUtils.ToSentinelKey(tag));
                }
            }

            // Initialize a flag to track whether the item is invalidated based on tag checks
            bool result = false;

            // Check if there are any tags that need to be checked against the distributed cache
            if (searchList.Count > 0)
            {
                // Generate a list of sentinel keys and retrieve their associated invalidation timestamps from the distributed cache in bulk
                var sentinelList = _distributedCache.GetBulk<long>(searchList.ToArray());

                // Iterate through the retrieved sentinel values from the distributed cache
                foreach (var sentinel in sentinelList)
                {
                    // Add the retrieved invalidation timestamp for the tag to the in-memory dictionary for future reference
                    _tagInvalidationTimes[NCacheHybridCacheUtils.FromSentinelKey(sentinel.Key)] = Task.FromResult(sentinel.Value);

                    // Check if the retrieved invalidation timestamp for the tag is greater than or equal to the item's creation timestamp
                    if (sentinel.Value >= creationTimestampUnixMs)
                        // If true, the item is invalidated due to this tag, so we can set the result to true
                        result = true;
                }
            }
            return result;
        }

        /// <summary>
        /// Releases all resources used by the current instance of the class.
        /// </summary>
        /// <remarks>Call this method when the instance is no longer needed to free associated resources.
        /// After calling this method, the instance should not be used.</remarks>
        public void Dispose()
        {
            // Clear the in-memory tag invalidation times
            _tagInvalidationTimes.Clear();
        }

        #endregion
    }
}