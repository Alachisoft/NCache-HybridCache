using Alachisoft.NCache.Client;
using Alachisoft.NCache.Runtime.Caching;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using System.Drawing;
using CacheItem = Alachisoft.NCache.Client.CacheItem;

namespace NCache.OSS.Caching.Hybrid
{
    public sealed class NCacheHybridCache : HybridCache, IDisposable
    {
        #region Private Fields

        /// <summary>
        /// A unique identifier for this cache instance, used to prevent processing its own cache update messages. This is typically constructed using the client IP address and client ID to ensure uniqueness across different instances of the cache.
        /// </summary>
        private readonly string _identifier;

        /// <summary>
        /// Represents the configuration settings for the hybrid cache.
        /// </summary>
        private readonly NCacheHybridCacheOptions _configuration;

        /// <summary>
        /// Represents the topic used for publishing and subscribing to cache update notifications. This topic is used to synchronize cache updates across different instances of the cache by allowing them to publish messages when cache entries are added, updated, or removed, and to subscribe to receive those messages for local cache synchronization.
        /// </summary>
        private readonly ITopic _topic;

        /// <summary>
        /// A topic name for cache update notifications. This is typically derived from the client ID to ensure that it is unique across different instances of the cache, allowing for proper synchronization of cache updates without conflicts. The topic name is used when publishing cache update messages to notify other instances to update or invalidate their local caches accordingly.
        /// </summary>
        private readonly string _topicName;

        /// <summary>
        /// Local in-memory cache (L1)
        /// </summary>
        private readonly ICache _memoryCache;

        /// <summary>
        /// Remote distributed cache (L2)
        /// </summary>
        private readonly ICache _distributedCache;

        /// <summary>
        /// Instance of ILogger for logging cache operations and events.
        /// </summary>
        private readonly ILogger<NCacheHybridCache> _logger;

        /// <summary>
        /// A thread-safe dictionary that maps request identifiers to their associated semaphore locks.
        /// </summary>
        private readonly ConcurrentDictionary<string, SemaphoreSlim> _reqLocks = new ConcurrentDictionary<string, SemaphoreSlim>();

        /// <summary>
        /// Provides access to the TagKeeper instance used for handling tag invalidation operations.
        /// </summary>
        private readonly TagKeeper _tagKeeper;

        #endregion Private Fields

        #region Constructor and private methods

        /// <summary>
        /// Initializes a new instance of the NCacheHybridCache class using the specified distributed cache configuration.
        /// Sets up both local and distributed caches to enable hybrid caching functionality.
        /// </summary>
        /// <remarks>The local and distributed cache names are derived from the provided configuration options. The instance
        /// is uniquely identified using the client IP address and client ID to manage cache update notifications and
        /// avoid processing its own updates.</remarks>
        /// <param name="options">The configuration options containing settings for both local and distributed caches, including cache names and other
        /// relevant parameters required for cache setup.</param>
        /// <param name="logger">Optional logger instance for logging cache operations and events. If not provided or logging is disabled in configuration, a null logger will be used.</param>
        /// <exception cref="InvalidOperationException">Thrown if either the local or distributed cache cannot be initialized, indicating a critical configuration
        /// issue that prevents the cache from functioning.</exception>
        internal NCacheHybridCache(IOptions<NCacheHybridCacheOptions> options, ILogger<NCacheHybridCache>? logger = null)
        {
            #region Configuration validation and Logger setup

            // Extract the configuration values from the provided options.
            _configuration = options.Value;

            // Set the logger if logger is available and configuration enabled otherwise set a null logger to avoid exceptions
            _logger = logger != null ? logger : global::Microsoft.Extensions.Logging.Abstractions.NullLogger<NCacheHybridCache>.Instance;

            // Check if the configuration is valid.
            if (!_configuration.isValid(out var configError))
            {
                // if configuration is not valid, log the error and throw an exception to prevent cache initialization with invalid settings.
                _logger.LogError(configError);
                throw new InvalidOperationException(configError);
            }

            #endregion

            #region L1 Cache initialization

            try
            {
                // Initialize local in-memory cache (L1 cache) based on configuration.
                _memoryCache = CacheManager.GetCache(_configuration.LocalCacheName);

                // Validate that the local cache was successfully initialized. If not, throw an exception to indicate a critical configuration issue that needs to be addressed.
                if (_memoryCache == null)
                {
                    var err = $"Failed to initialize local cache with name '{_configuration.LocalCacheName}'. Ensure that the cache is properly configured and available.";
                    _logger.LogError(err);
                    throw new InvalidOperationException(err);
                }

                // Log successful initialization of the local cache for better observability and troubleshooting.
                _logger.LogInformation($"Successfully initialized local cache with name '{_configuration.LocalCacheName}'.");
            }
            catch (Exception ex)
            {
                var err = $"An error occurred while initializing the local cache with name '{_configuration.LocalCacheName}'. Ensure that the cache is properly configured and available. Exception: {ex.Message}";
                _logger.LogError(ex, err);
                throw new InvalidOperationException(err, ex);
            }

            #endregion

            #region L2 Cache initialization

            try
            {
                // Initialize the distributed cache (L2) based on configuration with connection options.
                _distributedCache = CacheManager.GetCache(_configuration.DistributedCacheName, _configuration.GetCacheConnectionOptions());

                // Validate that the distributed cache was successfully initialized. If not, throw an exception to indicate a critical configuration issue that needs to be addressed.
                if (_distributedCache == null)
                {
                    var err = $"Failed to initialize distributed cache with name '{_configuration.DistributedCacheName}'. Ensure that the cache is properly configured and available.";
                    _logger.LogError(err);
                    throw new InvalidOperationException(err);
                }

                // Log successful initialization of the distributed cache for better observability and troubleshooting.
                _logger.LogInformation($"Successfully initialized distributed cache with name '{_configuration.DistributedCacheName}'.");
            }
            catch (Exception ex)
            {
                var err = $"An error occurred while initializing the distributed cache with name '{_configuration.DistributedCacheName}'. Ensure that the cache is properly configured and available. Exception: {ex.Message}";
                _logger.LogError(ex, err);
                throw new InvalidOperationException(err, ex);
            }

            #endregion

            #region TagKeeper and messaging initialization

            try
            {
                // Initialize TagKeeper with the distributed cache and a predefined expiration time for tag sentinels
                _tagKeeper = new TagKeeper(_distributedCache, _configuration);

                // Set unique identifier for this instance to avoid processing its own messages
                Guid uniqueId = Guid.NewGuid();
                _identifier = NCacheHybridCacheUtils.GetIdentifier(
                    _distributedCache,
                    _configuration,
                    uniqueId);

                // Set topic name for cache messaging based on configuration
                _topicName = NCacheHybridCacheUtils.GetTopicName(_configuration);

                // Fetch topic. If the topic does not exist, create it
                _topic = _distributedCache.MessagingService.GetTopic(_topicName);
                if (_topic == null)
                {
                    _topic = _distributedCache.MessagingService.CreateTopic(_topicName);
                }

                // Subscribe to the topic to receive cache update notifications from other instances
                var subscription = _topic.CreateSubscription(SyncL1Callback);

                // Log successful initialization of the messaging topic for cache update notifications.
                _logger.LogInformation($"Successfully initialized NCacheHybridCache");
            }
            catch (Exception ex)
            {
                var err = $"An error occurred while initializing HybridCache. Ensure that the configuration is correct and the cache is available.";
                _logger.LogError(err, ex);
                throw new InvalidOperationException(err);
            }

            #endregion
        }

        /// <summary>
        /// Handles a subscription event by processing the received message data.
        /// </summary>
        /// <remarks>Intended to be used as a callback for message subscription events. Ensure that this
        /// handler is registered to receive relevant messages.</remarks>
        /// <param name="sender">The source of the event, typically the publisher that triggered the message event.</param>
        /// <param name="args">An object containing the event data, including details about the received message.</param>
        private void SyncL1Callback(object sender, MessageEventArgs args)
        {
            _logger.LogDebug("Received message with ID '{MessageId}'", args.Message?.MessageId);

            if (args.Message != null && args.Message.Payload != null && args.Message.Payload is NCacheHybridCacheMessage)
            {
                _logger.LogDebug("Processing message with ID '{MessageId}' and payload of type '{PayloadType}'", args.Message.MessageId, args.Message.Payload.GetType().Name);

                // Cast the message payload to the expected type for processing cache update notifications
                var message = (NCacheHybridCacheMessage)args.Message.Payload;

                // Check if the message originated from a different instance by comparing the message identifier with this instance's identifier
                if (message.Identifier != _identifier)
                {
                    _logger.LogDebug("Message with ID '{MessageId}' originated from a different instance, processing started", args.Message.MessageId);

                    // Check if message is related to cache update (Remove/Update)
                    if (message.Entities != null && message.Entities.Any() && (message.Type == HybridCacheMessageType.Remove || message.Type == HybridCacheMessageType.Update))
                    {
                        try
                        {
                            // Process the message based on its type
                            switch (message.Type)
                            {
                                case HybridCacheMessageType.Remove:

                                    // Get the cache items from local cache for the entities (cache keys) specified in the message
                                    var items = _memoryCache.GetCacheItemBulk(message.Entities);

                                    // Check if the entity (cache key) exists in local cache
                                    if (items != null && items.Any())
                                    {
                                        _logger.LogDebug("Processing Remove message with ID '{MessageId}' for entity '{Entity}'", args.Message.MessageId, message.Entities);

                                        // Removing the item from local cache
                                        _memoryCache.RemoveBulk(message.Entities);

                                        _logger.LogInformation("Removed entity '{Entity}' from local cache in response to message with ID '{MessageId}'", message.Entities, args.Message.MessageId);
                                    }
                                    else
                                    {
                                        _logger.LogDebug("Entity '{Entity}' not found in local cache while processing message with ID '{MessageId}'", message.Entities, args.Message.MessageId);
                                    }

                                    break;

                                case HybridCacheMessageType.Update:

                                    _logger.LogDebug("Processing Update message with ID '{MessageId}' for entity '{Entity}'", args.Message.MessageId, message.Entities);

                                    // Get the updated item from distributed cache for the entity (cache key) specified in the message
                                    var newItems = _distributedCache.GetCacheItemBulk(message.Entities);

                                    if (newItems != null && newItems.Any())
                                    {
                                        // Set expiration for all items if configured
                                        if (_configuration.LocalCacheExpiration.HasValue)
                                        {
                                            foreach (var item in newItems.Values)  // Directly iterate CacheItem objects
                                            {
                                                item.Expiration = new Expiration(ExpirationType.Absolute, _configuration.LocalCacheExpiration.Value);
                                            }
                                        }

                                        // Update the item in local cache with the value from distributed cache for the specified entity (cache key)
                                        _memoryCache.InsertBulk(newItems);

                                        _logger.LogInformation("Updated entity '{Entity}' in local cache from distributed cache in response to message with ID '{MessageId}'", message.Entities, args.Message.MessageId);
                                    }
                                    else
                                    {
                                        _logger.LogDebug("Entity '{Entity}' not found in distributed cache while processing message with ID '{MessageId}'", message.Entities, args.Message.MessageId);
                                    }

                                    break;
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error processing message with ID '{MessageId}' for entity '{Entity}'", args.Message.MessageId, message.Entities);
                        }
                    }
                    // Check if message is related to tag invalidation (Tag/WildCard)
                    else if (message.Entities != null && (message.Type == HybridCacheMessageType.Tag || message.Type == HybridCacheMessageType.WildCard))
                    {
                        try
                        {
                            // Process the message based on its type
                            switch (message.Type)
                            {
                                case HybridCacheMessageType.Tag:
                                    _logger.LogDebug("Processing Tag message with ID '{MessageId}' for entity '{Entity}'", args.Message.MessageId, message.Entities);

                                    // Synchronize the specific tag in TagKeeper from distributed cache
                                    _tagKeeper.SyncL1TagFromL2(message.Entities, CancellationToken.None).Wait();

                                    _logger.LogInformation("Synchronized tag '{Entity}' in TagKeeper from distributed cache in response to message with ID '{MessageId}'", message.Entities, args.Message.MessageId);
                                    break;

                                case HybridCacheMessageType.WildCard:
                                    _logger.LogDebug("Processing WildCard message with ID '{MessageId}'", args.Message.MessageId);

                                    // Synchronize wildcard tags in TagKeeper from distributed cache
                                    _tagKeeper.SyncWildCardFromL2(CancellationToken.None).Wait();

                                    _logger.LogInformation("Synchronized wildcard tags in TagKeeper from distributed cache in response to message with ID '{MessageId}'", args.Message.MessageId);
                                    break;
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error processing message with ID '{MessageId}' for entity '{Entity}'", args.Message.MessageId, message.Entities);
                        }
                    }
                    // If message type is not recognized, log it for debugging purposes
                    else
                    {
                        _logger.LogDebug("Message with ID '{MessageId}' has invalid data, skipping processing", args.Message.MessageId);
                    }
                }
                else
                {
                    _logger.LogDebug("Ignoring message with ID '{MessageId}' as it originated from this instance", args.Message.MessageId);
                }
            }
        }

        /// <summary>
        /// Publishes a cache update notification message to the associated topic, indicating whether the specified
        /// cache entry has been added or removed.
        /// </summary>
        /// <remarks>The notification message includes the cache identifier, the entry key, and the update
        /// status. Subscribers to the topic can use this information to synchronize their cache state
        /// accordingly.</remarks>
        /// <param name="key">The unique identifier of the cache entry to be updated.</param>
        /// <param name="isUpdate">A value indicating whether the operation is an update (<see langword="true"/>) or a removal (<see
        /// langword="false"/>) of the cache entry.</param>
        private void PublishCacheUpdate(string key, bool isUpdate)
        {
            _logger.LogDebug("Publishing cache {Operation} for key '{Key}'", isUpdate ? "update" : "removal", key);

            try
            {
                // Create a payload for the cache update/remove message
                var payload = new NCacheHybridCacheMessage(_identifier, key, isUpdate ? HybridCacheMessageType.Update : HybridCacheMessageType.Remove);

                // Create a new message with the payload to be published to the topic
                var msg = new Message(payload);

                // Publish the message to the topic with DeliveryOption.All to ensure that all subscribers receive the message
                _topic.Publish(msg, DeliveryOption.All);

                _logger.LogInformation("Published cache {Operation} for key '{Key}'", isUpdate ? "update" : "removal", key);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while publishing cache {Operation} for key '{Key}'", isUpdate ? "update" : "removal", key);
            }
        }

        /// <summary>
        /// Publishes a cache update or removal notification for the specified cache keys to all subscribers.
        /// </summary>
        /// <remarks>This method ensures that all subscribers to the cache topic are notified of the
        /// specified cache operation. Use this method to propagate cache changes across distributed systems.</remarks>
        /// <param name="keys">The collection of cache keys for which the update or removal notification is to be published. Cannot be null
        /// or empty.</param>
        /// <param name="isUpdate">A value indicating whether the notification is for an update (<see langword="true"/>) or a removal (<see
        /// langword="false"/>) of the cache entries.</param>
        private void PublishCacheUpdate(IEnumerable<string> keys, bool isUpdate)
        {
            _logger.LogDebug("Publishing cache {Operation} for keys '{Keys}'", isUpdate ? "update" : "removal", string.Join(", ", keys));

            try
            {
                // Create a payload for the cache update/remove message
                var payload = new NCacheHybridCacheMessage(_identifier, keys, isUpdate ? HybridCacheMessageType.Update : HybridCacheMessageType.Remove);

                // Create a new message with the payload to be published to the topic
                var msg = new Message(payload);

                // Publish the message to the topic with DeliveryOption.All to ensure that all subscribers receive the message
                _topic.Publish(msg, DeliveryOption.All);

                _logger.LogInformation("Published cache {Operation} for keys '{Keys}'", isUpdate ? "update" : "removal", string.Join(", ", keys));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while publishing cache {Operation} for keys '{Keys}'", isUpdate ? "update" : "removal", string.Join(", ", keys));
            }
        }

        /// <summary>
        /// Publishes a cache invalidation notification for the specified tag to all subscribers.
        /// </summary>
        /// <remarks>This method ensures that all subscribers are notified to invalidate their local
        /// caches for the given tag. Use this method when cache consistency across distributed components is
        /// required.</remarks>
        /// <param name="tag">The cache tag to invalidate. Cannot be null or empty.</param>
        private void PublishTagInvalidation(string tag)
        {
            bool isWildcard = tag == NCacheHybridCacheUtils.Wildcard;

            _logger.LogDebug("Publishing {TagType} invalidation for tag '{Tag}'", isWildcard ? "wildcard tag" : "tag", tag);

            try
            {
                // Create a payload for the tag invalidation message
                var payload = new NCacheHybridCacheMessage(_identifier, tag, isWildcard ? HybridCacheMessageType.WildCard : HybridCacheMessageType.Tag);

                // Create a new message with the payload to be published to the topic
                var msg = new Message(payload);

                // Publish the message to the topic with DeliveryOption.All to ensure that all subscribers receive the invalidation message
                _topic.Publish(msg, DeliveryOption.All);

                _logger.LogInformation("Published {TagType} invalidation for tag '{Tag}'", isWildcard ? "wildcard tag" : "tag", tag);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while publishing {TagType} invalidation for tag '{Tag}'", isWildcard ? "wildcard tag" : "tag", tag);
            }
        }

        /// <summary>
        /// Publishes cache invalidation notifications for the specified tags to all subscribers.
        /// </summary>
        /// <remarks>This method ensures that all subscribers are notified to invalidate their local
        /// caches for the given tags. Use this method when cache consistency across distributed components is
        /// required for multiple tags.</remarks>
        /// <param name="tags">The collection of cache tags to invalidate. Cannot be null or empty.</param>
        private void PublishTagInvalidation(IEnumerable<string> tags)
        {
            _logger.LogDebug("Publishing tag invalidation for tags '{Tags}'", string.Join(", ", tags));

            try
            {
                // Create a payload for the tag invalidation message
                var payload = new NCacheHybridCacheMessage(_identifier, tags, HybridCacheMessageType.Tag);

                // Create a new message with the payload to be published to the topic
                var msg = new Message(payload);

                // Publish the message to the topic with DeliveryOption.All to ensure that all subscribers receive the invalidation message
                _topic.Publish(msg, DeliveryOption.All);

                _logger.LogInformation("Published tag invalidation for tags '{Tags}'", string.Join(", ", tags));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while publishing tag invalidation for tags '{Tags}'", string.Join(", ", tags));
            }
        }

        #endregion Constructor and private methods

        #region Public API implementation

        /// <summary>
        /// Retrieves the value associated with the specified key from the cache, or creates and adds a new value using
        /// the provided factory function if the key does not exist or is invalidated.
        /// </summary>
        /// <remarks>This method checks both local and distributed caches for the specified key, honoring
        /// cache flags and tag-based invalidation. If the value is not found or is invalidated, the factory function is
        /// invoked to create a new value, which is then cached according to the provided options. Thread safety is
        /// ensured to prevent multiple concurrent factory invocations for the same key. If caching is disabled by
        /// flags, the method may return the default value of T without invoking the factory.</remarks>
        /// <typeparam name="TState">The type of the state object passed to the factory function.</typeparam>
        /// <typeparam name="T">The type of the value to retrieve or create.</typeparam>
        /// <param name="key">The unique key that identifies the cache entry. Cannot be null or empty unless underlying data fetch is
        /// disabled by flags.</param>
        /// <param name="state">A state object to pass to the factory function when creating a new value.</param>
        /// <param name="factory">A function that produces the value to cache if the key is not found or is invalidated. The function receives
        /// the state object and a cancellation token.</param>
        /// <param name="options">Optional cache entry options that control expiration, cache behavior, and flags. If null, default options
        /// are used.</param>
        /// <param name="tags">An optional collection of tags to associate with the cache entry for invalidation or grouping purposes.</param>
        /// <param name="cancellationToken">A cancellation token that can be used to cancel the asynchronous operation.</param>
        /// <returns>A ValueTask that represents the asynchronous operation. The result contains the cached value if found and
        /// valid, or the value produced by the factory function. Returns the default value of T if the key is null or
        /// empty and underlying data fetch is disabled.</returns>
        public override ValueTask<T> GetOrCreateAsync<TState, T>(string key, TState state, Func<TState, CancellationToken, ValueTask<T>> factory, HybridCacheEntryOptions options = null, IEnumerable<string> tags = null, CancellationToken cancellationToken = default)
        {
            // Set default flags if options is null
            var flags = options?.Flags ?? HybridCacheEntryFlags.None;

            #region No Cache operation

            // Check if the key is null or empty or BOTH L1 and L2 cache reads are disabled by flags
            if (string.IsNullOrEmpty(key) || ((flags & HybridCacheEntryFlags.DisableLocalCacheRead) != 0 && (flags & HybridCacheEntryFlags.DisableDistributedCacheRead) != 0))
            {
                _logger.LogInformation("GetOrCreateAsync called with null or empty key, or both L1 and L2 reads disabled.");

                // Check if underlying data fetch is disabled by flag
                if ((flags & HybridCacheEntryFlags.DisableUnderlyingData) == 0)
                {
                    // Invoke the factory to get the value without caching it.
                    return new ValueTask<T>(Task<T>.Run(async () =>
                    {
                        try
                        {
                            // Invoke the factory function to get the fresh value to be cached and returned
                            return await factory(state, cancellationToken).AsTask().ConfigureAwait(false);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error occurred while invoking factory for key '{Key}'", key);
                            throw;
                        }
                    }, cancellationToken));
                }
                else
                {
                    _logger.LogDebug("GetOrCreateAsync called with null or empty key and underlying data fetch is disabled by flags, returning default value.");
                    return new ValueTask<T>(default(T)!);
                }
            }

            #endregion

            _logger.LogInformation("GetOrCreateAsync called for key '{Key}'", key);
            _logger.LogDebug("GetOrCreateAsync flags: {Flags}, tags: {Tags}", flags, tags == null ? "null" : string.Join(',', tags));

            // Create and return a ValueTask that runs the cache retrieval and creation logic asynchronously.
            return new ValueTask<T>(Task.Run(async () =>
            {
                // Flag to track if the cache item is invalid due to tag invalidation.
                bool isItemInvalid = false;

                #region Fetching data from local cache

                // Try L1 (Local Cache) first if local cache read is not disabled by flags.
                if ((flags & HybridCacheEntryFlags.DisableLocalCacheRead) == 0)
                {
                    _logger.LogDebug("Attempting L1 read for key '{Key}'", key);
                    var localItem = _memoryCache.Get<NCacheHybridCacheItem>(key);

                    // Check if the item exists in local cache
                    if (localItem != null)
                    {
                        _logger.LogInformation("L1 cache hit for key '{Key}'", key);

                        // Fetch tags invalidation status from TagKeeper to determine if the item is invalidated by any of its associated tags.
                        isItemInvalid = await _tagKeeper.IsItemInvalidatedAsync(localItem.CreatedTimeStamp, localItem.Tags, cancellationToken);

                        // Check if the item is invalidated by tags.
                        if (!isItemInvalid)
                        {
                            _logger.LogDebug("Returning value from L1 for key '{Key}'", key);

                            try
                            {
                                // Return value from local cache if it is not invalidated.
                                return localItem!.GetValue<T>();
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "Error occurred while retrieving value from L1 cache for key '{Key}'", key);
                                throw;
                            }
                        }
                        else
                        {
                            _logger.LogInformation("L1 entry for key '{Key}' invalidated by tags", key);
                        }
                    }
                    else
                    {
                        _logger.LogInformation("L1 cache miss for key '{Key}'", key);
                    }
                }
                else
                {
                    _logger.LogDebug("L1 cache read is disabled by flags for key '{Key}'", key);
                }

                #endregion

                #region Fetching data from remote cache

                // Check L2 (Distributed Cache) if distributed cache read is not disabled by flags.
                // We check L2 even if L1 item was invalidated, as L2 might have a fresher version.
                if ((flags & HybridCacheEntryFlags.DisableDistributedCacheRead) == 0)
                {
                    _logger.LogDebug("Attempting L2 read for key '{Key}'", key);

                    // Try to get the item from distributed cache (L2 cache)
                    var remoteItem = _distributedCache.Get<NCacheHybridCacheItem>(key);

                    // Check if the item exists in distributed cache
                    if (remoteItem != null)
                    {
                        _logger.LogInformation("L2 cache hit for key '{Key}'", key);

                        // Fetch tags invalidation status from TagKeeper to determine if the item is invalidated by any of its associated tags.
                        isItemInvalid = await _tagKeeper.IsItemInvalidatedAsync(remoteItem.CreatedTimeStamp, remoteItem.Tags, cancellationToken);

                        // Check if the item is invalidated by tags.
                        if (!isItemInvalid)
                        {
                            // Check if local cache write is not disabled by flags
                            if ((flags & HybridCacheEntryFlags.DisableLocalCache) == 0)
                            {
                                _logger.LogDebug("Populating L1 for key '{Key}' from L2", key);

                                // Create a new cache item for local cache with the value from remote cache and associated tags.
                                var cacheItem = new CacheItem(remoteItem);

                                // Set expiration for the cache item if specified in options.
                                if (options != null && options.LocalCacheExpiration.HasValue)
                                    cacheItem.Expiration = new Expiration(ExpirationType.Absolute, options.LocalCacheExpiration.Value);
                                // If expiration is not specified in options, use the default local cache expiration from configuration if available.
                                else if (_configuration.LocalCacheExpiration.HasValue)
                                    cacheItem.Expiration = new Expiration(ExpirationType.Absolute, _configuration.LocalCacheExpiration.Value);

                                // Populate local cache with the value from remote cache for future requests.
                                _memoryCache.Insert(key, cacheItem);
                            }
                            else
                            {
                                _logger.LogDebug("Local cache write is disabled by flags, skipping L1 population for key '{Key}'", key);
                            }
                            _logger.LogDebug("Returning value from L2 for key '{Key}'", key);

                            try
                            {
                                // Return value from remote cache if it is not invalidated
                                return remoteItem!.GetValue<T>();
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "Error occurred while retrieving value from L2 cache for key '{Key}'", key);
                                throw;
                            }
                        }
                        else
                        {
                            _logger.LogInformation("L2 entry for key '{Key}' invalidated by tag(s)", key);
                        }
                    }
                    else
                    {
                        _logger.LogInformation("L2 cache miss for key '{Key}'", key);
                    }
                }
                else
                {
                    _logger.LogDebug("L2 cache read is disabled by flags for key '{Key}'", key);
                }

                #endregion

                // Check if underlying data fetch is not disabled by flags before invoking factory to get fresh value and populate cache
                if ((flags & HybridCacheEntryFlags.DisableUnderlyingData) == 0)
                {
                    #region Locking logic to prevent Cache Stampede

                    // Create or get a semaphore for the given key to ensure that only one request can fetch and populate the cache for a missing key at a time
                    var semaphore = _reqLocks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));

                    _logger.LogDebug("Waiting on semaphore for key '{Key}'", key);

                    // Wait to acquire the semaphore before proceeding to fetch and populate the cache
                    await semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);

                    _logger.LogDebug("Acquired semaphore for key '{Key}'", key);

                    #endregion

                    try
                    {
                        #region Re-check cache after lock release

                        // Check if L1 read is disabled by flags before re-checking L1 cache for the item after acquiring the lock
                        if ((flags & HybridCacheEntryFlags.DisableLocalCacheRead) == 0)
                        {
                            // All cache-miss requests after the first can get data from L2 cache
                            var recheckL1Item = _memoryCache.Get<NCacheHybridCacheItem>(key);

                            // Check if item was found
                            if (recheckL1Item != null)
                            {
                                // Check if item is invalidated by tags
                                isItemInvalid = _tagKeeper.IsItemInvalidatedAsync(recheckL1Item.CreatedTimeStamp, recheckL1Item.Tags, cancellationToken).Result;

                                // If item is not invalid, return the value from L1 cache
                                if (!isItemInvalid)
                                {
                                    _logger.LogInformation("L1 hit after lock for key '{Key}'", key);
                                    return recheckL1Item!.GetValue<T>();
                                }
                            }
                        }
                        // No need to check if L2 read is also disabled by flags. Already handled at the beginning of the method
                        else
                        {
                            // If item was not saved in L1 due to flag, checking L2
                            var recheckL2Item = _distributedCache.Get<NCacheHybridCacheItem>(key);

                            // Check if item was found
                            if (recheckL2Item != null)
                            {
                                // Check if item is invalidated by tags
                                isItemInvalid = _tagKeeper.IsItemInvalidatedAsync(recheckL2Item.CreatedTimeStamp, recheckL2Item.Tags, cancellationToken).Result;

                                // If item is not invalid, return the value from L2 cache
                                if (!isItemInvalid)
                                {
                                    _logger.LogInformation("L2 hit after lock for key '{Key}'", key);
                                    return recheckL2Item!.GetValue<T>();
                                }
                            }
                        }
                        #endregion

                        #region Factory invocation to get fresh value

                        _logger.LogInformation("Cache miss for key '{Key}', invoking factory", key);

                        // Declare a variable to hold the fresh value that will be obtained from the factory function
                        T freshValue;

                        try
                        {
                            // Invoke the factory function to get the fresh value to be cached and returned
                            freshValue = await factory(state, cancellationToken).ConfigureAwait(false);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error occurred while invoking factory for key '{Key}'", key);
                            throw;
                        }

                        #endregion

                        try
                        {
                            #region Populate L2 cache with fresh value

                            // Cache the fresh value in distributed cache first
                            if ((flags & HybridCacheEntryFlags.DisableDistributedCacheWrite) == 0 && (flags & HybridCacheEntryFlags.DisableDistributedCache) == 0)
                            {
                                _logger.LogDebug("Inserting key '{Key}' into L2 from factory", key);

                                // create a new cache item for distributed cache with the fresh value and associated tags.
                                var cacheItem = new CacheItem(new NCacheHybridCacheItem(freshValue!, tags));
                                if (options != null && options.Expiration.HasValue)
                                    cacheItem.Expiration = new Expiration(ExpirationType.Absolute, options.Expiration.Value);
                                // If expiration is not specified in options, use the default distributed cache expiration from configuration if available.
                                else if (_configuration.DistributedCacheExpiration.HasValue)
                                    cacheItem.Expiration = new Expiration(ExpirationType.Absolute, _configuration.DistributedCacheExpiration.Value);

                                // Insert the fresh value into the distributed cache (L2)
                                _distributedCache.Insert(key, cacheItem);

                                PublishCacheUpdate(key, true);

                                _logger.LogInformation("Inserted key '{Key}' into L2 from factory", key);
                            }

                            #endregion

                            #region Populate L1 cache with fresh value

                            if ((flags & HybridCacheEntryFlags.DisableLocalCacheWrite) == 0 && (flags & HybridCacheEntryFlags.DisableLocalCache) == 0)
                            {
                                _logger.LogDebug("Inserting key '{Key}' into L1", key);

                                // create a new cache item for local cache with the fresh value and associated tags.
                                var cacheItem = new CacheItem(new NCacheHybridCacheItem(freshValue!, tags));

                                // Set expiration for the cache item if specified in options.
                                if (options != null && options.LocalCacheExpiration.HasValue)
                                    cacheItem.Expiration = new Expiration(ExpirationType.Absolute, options.LocalCacheExpiration.Value);
                                // If expiration is not specified in options, use the default local cache expiration from configuration if available.
                                else if (_configuration.LocalCacheExpiration.HasValue)
                                    cacheItem.Expiration = new Expiration(ExpirationType.Absolute, _configuration.LocalCacheExpiration.Value);

                                // Insert the fresh value into the local cache (L1) for future requests
                                _memoryCache.Insert(key, cacheItem);

                                _logger.LogInformation("Inserted key '{Key}' into L1 from factory", key);
                            }

                            #endregion
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error occurred while inserting key '{Key}' into cache after factory invocation", key);
                            throw;
                        }

                        _logger.LogInformation("Returning fresh value for key '{Key}'", key);

                        // Return the fresh value obtained from the factory function
                        return freshValue;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error occurred while processing GetOrCreateAsync for key '{Key}'", key);
                        throw;
                    }
                    finally
                    {
                        // Release the semaphore to allow other requests waiting for the same key to proceed
                        semaphore.Release();

                        _logger.LogDebug("Released semaphore for key '{Key}'", key);

                        // Remove the semaphore from the dictionary to prevent memory leaks
                        _reqLocks.TryRemove(key, out _);
                    }
                }
                else
                {
                    _logger.LogDebug("Underlying data fetch is disabled by flags for key '{Key}', returning default value", key);
                    return default!;
                }
            }, cancellationToken));
        }

        /// <summary>
        /// Asynchronously removes the specified cache entry from both the local and distributed caches.
        /// </summary>
        /// <remarks>If the specified key is found, the method removes it from both the local and
        /// distributed caches and notifies other instances to remove the key from their local caches. If the key is
        /// null or empty, no action is taken.</remarks>
        /// <param name="key">The key of the cache entry to remove. If the key is null or empty, the method completes without performing
        /// any operation.</param>
        /// <param name="cancellationToken">A cancellation token that can be used to cancel the remove operation.</param>
        /// <returns>A ValueTask that represents the asynchronous remove operation.</returns>
        public override ValueTask RemoveAsync(string key, CancellationToken cancellationToken = default)
        {
            #region Input validation

            // Return default if key is null or empty
            if (string.IsNullOrEmpty(key))
            {
                _logger.LogInformation("RemoveAsync called with null or empty key.");
                return default;
            }

            #endregion

            _logger.LogInformation("RemoveAsync called for key '{Key}'", key);

            return new ValueTask(Task.Run(async () =>
            {
                try
                {
                    // Remove the key from local cache
                    _logger.LogDebug("Removing key '{Key}' from local cache", key);
                    _memoryCache.Remove(key);

                    // Remove the key from distributed cache
                    _logger.LogDebug("Removing key '{Key}' from distributed cache", key);
                    _distributedCache.Remove(key);

                    // publish cache update notification to inform other instances to remove the key from their local caches
                    PublishCacheUpdate(key, false);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred while removing key '{Key}' from cache", key);
                    throw;
                }

                _logger.LogInformation("RemoveAsync completed for key '{Key}'", key);
            }, cancellationToken));
        }

        /// <summary>
        /// Asynchronously removes the specified keys from both the local and distributed caches.
        /// </summary>
        /// <remarks>This method removes the specified keys from both the local in-memory cache and the
        /// distributed cache. It also publishes a cache update notification to ensure other instances remove the keys
        /// from their local caches. If the keys collection is null or empty, no action is taken.</remarks>
        /// <param name="keys">The collection of cache keys to remove. If null or empty, the method completes without performing any operation.</param>
        /// <param name="cancellationToken">A cancellation token that can be used to cancel the remove operation.</param>
        /// <returns>A ValueTask that represents the asynchronous remove operation.</returns>
        public override ValueTask RemoveAsync(IEnumerable<string> keys, CancellationToken cancellationToken = default)
        {
            #region Input validation

            // Return default if keys is null or empty
            if (keys == null || !keys.Any())
            {
                _logger.LogInformation("RemoveAsync called with null or empty keys.");
                return default;
            }

            #endregion

            _logger.LogInformation("RemoveAsync called for keys '{Keys}'", string.Join(", ", keys));

            return new ValueTask(Task.Run(async () =>
            {
                try
                {
                    // Remove the keys from local cache
                    _logger.LogDebug("Removing keys '{Keys}' from local cache", string.Join(", ", keys));
                    _memoryCache.RemoveBulk(keys);

                    // Remove the keys from distributed cache
                    _logger.LogDebug("Removing keys '{Keys}' from distributed cache", string.Join(", ", keys));
                    _distributedCache.RemoveBulk(keys);

                    // publish cache update notification to inform other instances to remove the keys from their local caches
                    PublishCacheUpdate(keys, false);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred while removing keys '{Keys}' from cache", string.Join(", ", keys));
                    throw;
                }

                _logger.LogInformation("RemoveAsync completed for keys '{Keys}'", string.Join(", ", keys));
            }, cancellationToken));
        }

        /// <summary>
        /// Asynchronously invalidates all cache entries associated with the specified tag using logical deletion.
        /// </summary>
        /// <remarks>This method does not physically remove cache entries from local or distributed caches. Instead, it marks the tag as invalid
        /// in TagKeeper by updating its timestamp, causing future cache lookups to treat associated entries as invalidated. This approach maintains
        /// cache consistency across distributed systems without the overhead of physical removal. If the tag is null or empty, no action is taken.</remarks>
        /// <param name="tag">Tag associated with the cache entries to be invalidated. If the tag is null or empty, the method completes without performing any operation.</param>
        /// <param name="cancellationToken">Cancellation token that can be used to cancel the invalidation operation.</param>
        /// <returns>A ValueTask that represents the asynchronous tag invalidation operation.</returns>
        public override ValueTask RemoveByTagAsync(string tag, CancellationToken cancellationToken = default)
        {
            #region Input validation

            // Return default if tag is null or empty
            if (string.IsNullOrEmpty(tag))
            {
                _logger.LogInformation("RemoveByTagAsync called with null or empty tag.");
                return default;
            }

            #endregion

            _logger.LogInformation("RemoveByTagAsync called for tag '{Tag}'", tag);

            return new ValueTask(Task.Run(async () =>
            {
                try
                {
                    _logger.LogDebug("Marking tag '{Tag}' invalid in TagKeeper", tag);
                    await _tagKeeper.MarkTagInvalidAsync(tag, cancellationToken).ConfigureAwait(false);

                    // Publish tag invalidation message to inform other instances to invalidate their local cache entries associated with the tag
                    PublishTagInvalidation(tag);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred while removing cache entries by tag '{Tag}'", tag);
                    throw;
                }

                _logger.LogInformation("RemoveByTagAsync completed for tag '{Tag}'", tag);
            }, cancellationToken));
        }

        /// <summary>
        /// Asynchronously invalidates all cache entries associated with the specified tags using logical deletion.
        /// </summary>
        /// <remarks>This method marks all specified tags as invalid in TagKeeper by updating their timestamps, causing future cache lookups
        /// to treat associated entries as invalidated. It also notifies other instances to invalidate their local cache entries for the same tags.
        /// If the tags collection is null or empty, no action is taken.</remarks>
        /// <param name="tags">The collection of tags identifying the cache entries to invalidate. If null or empty, the method completes without performing any operation.</param>
        /// <param name="cancellationToken">A cancellation token that can be used to cancel the operation.</param>
        /// <returns>A ValueTask that represents the asynchronous tag invalidation operation.</returns>
        public override ValueTask RemoveByTagAsync(IEnumerable<string> tags, CancellationToken cancellationToken = default)
        {
            #region Input validation

            // Return default if tags is null or empty
            if (tags == null || !tags.Any())
            {
                _logger.LogInformation("RemoveByTagAsync called with null or empty tags.");
                return default;
            }

            #endregion

            _logger.LogInformation("RemoveByTagAsync called for tags '{Tags}'", string.Join(", ", tags));

            return new ValueTask(Task.Run(async () =>
            {
                try
                {
                    _logger.LogDebug("Marking tags '{Tags}' invalid in TagKeeper", string.Join(", ", tags));
                    await _tagKeeper.MarkTagInvalidAsync(tags, cancellationToken).ConfigureAwait(false);

                    // Publish tag invalidation message to inform other instances to invalidate their local cache entries associated with the tag
                    PublishTagInvalidation(tags);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred while removing cache entries by tag '{Tags}'", string.Join(", ", tags));
                    throw;
                }

                _logger.LogInformation("RemoveByTagAsync completed for tags '{Tags}'", string.Join(", ", tags));
            }, cancellationToken));
        }

        /// <summary>
        /// Method to asynchronously set a value in both the local and distributed caches using the specified key and options.
        /// </summary>
        /// <remarks>This method writes the value to both the local and distributed caches unless disabled
        /// by the provided options. Tags and expiration policies are applied according to the specified options. If
        /// certain cache layers are disabled via flags in the options, the value will only be written to the enabled
        /// layers.</remarks>
        /// <typeparam name="T">The type of the value to cache.</typeparam>
        /// <param name="key">The unique key under which the value is stored. Cannot be null or empty.</param>
        /// <param name="value">The value to cache. The type is specified by the generic parameter T.</param>
        /// <param name="options">Optional settings that control cache entry behavior, such as expiration policies and cache write flags. If
        /// null, default options are used.</param>
        /// <param name="tags">An optional collection of tags to associate with the cache entry for categorization or filtering.</param>
        /// <param name="cancellationToken">A cancellation token that can be used to cancel the asynchronous operation.</param>
        /// <returns>A task that represents the asynchronous cache set operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown if the key is null or empty.</exception>
        public override ValueTask SetAsync<T>(string key, T value, HybridCacheEntryOptions options = null, IEnumerable<string> tags = null, CancellationToken cancellationToken = default)
        {
            #region Input validation

            // Throw exception if key is null or empty
            if (string.IsNullOrEmpty(key))
            {
                _logger.LogError("SetAsync called with null or empty key.");
                throw new ArgumentNullException(nameof(key));
            }

            // Throw exception if value is null
            if (value == null)
            {     
                _logger.LogError("SetAsync called with null value for key '{Key}'.", key);
                throw new ArgumentNullException(nameof(value));
            }

            #endregion

            _logger.LogInformation("SetAsync called for key '{Key}'", key);

            // Create and return a ValueTask that runs cache set operations asynchronously. 
            return new ValueTask(Task.Run(async () =>
            {
                // Set default flags if options is null
                var flags = options?.Flags ?? HybridCacheEntryFlags.None;

                try
                {
                    _logger.LogDebug("SetAsync flags: {Flags}, tags: {Tags}", flags, tags == null ? "null" : string.Join(',', tags));

                    #region Populate remote cache

                    // Check if distributed cache write is not disabled by flags before writing to remote cache.
                    if ((flags & HybridCacheEntryFlags.DisableDistributedCacheWrite) == 0)
                    {
                        try
                        {
                            _logger.LogDebug("Inserting key '{Key}' into distributed cache", key);

                            // Create a new cache item for distributed cache with the provided value and associated tags.
                            var cacheItem = new CacheItem(new NCacheHybridCacheItem(value, tags));

                            // Set expiration for the cache item if specified in options.
                            if (options != null && options.Expiration.HasValue)
                                cacheItem.Expiration = new Expiration(ExpirationType.Absolute, options.Expiration.Value);

                            // If expiration is not specified in options, use the default distributed cache expiration from configuration if available.
                            else if (_configuration.DistributedCacheExpiration.HasValue)
                                cacheItem.Expiration = new Expiration(ExpirationType.Absolute, _configuration.DistributedCacheExpiration.Value);

                            // Insert the cache item into the distributed cache (L2)
                            _distributedCache.Insert(key, cacheItem);

                            // Publish cache update notification to inform other instances to update their local caches with the new value for the key
                            PublishCacheUpdate(key, true);

                            _logger.LogDebug("Inserted key '{Key}' into distributed cache", key);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error inserting key '{Key}' into distributed cache", key);
                        }
                    }
                    else
                    {
                        _logger.LogDebug("Distributed cache write is disabled by flags, skipping L2 population for key '{Key}'", key);
                    }

                    #endregion

                    #region Populate local cache

                    // Check if local cache write is not disabled by flags before writing to local cache.
                    if ((flags & HybridCacheEntryFlags.DisableLocalCacheWrite) == 0)
                    {
                        try
                        {
                            _logger.LogDebug("Inserting key '{Key}' into local cache", key);

                            // Create a new cache item for local cache with the provided value and associated tags.
                            var cacheItem = new CacheItem(new NCacheHybridCacheItem(value, tags));

                            // Set expiration for the cache item if specified in options.
                            if (options != null && options.LocalCacheExpiration.HasValue)
                                cacheItem.Expiration = new Expiration(ExpirationType.Absolute, options.LocalCacheExpiration.Value);
                            // If expiration is not specified in options, use the default local cache expiration from configuration if available.
                            else if (_configuration.LocalCacheExpiration.HasValue)
                                cacheItem.Expiration = new Expiration(ExpirationType.Absolute, _configuration.LocalCacheExpiration.Value);

                            // Insert the cache item into the local cache (L1) for future requests
                            _memoryCache.Insert(key, cacheItem);

                            _logger.LogDebug("Inserted key '{Key}' into local cache", key);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error inserting key '{Key}' into local cache", key);
                        }
                    }
                    else
                    {
                        _logger.LogDebug("Local cache write is disabled by flags, skipping L1 population for key '{Key}'", key);
                    }

                    #endregion
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unhandled exception in SetAsync for key '{Key}'", key);
                    throw;
                }
            }, cancellationToken));
        }

        /// <summary>
        /// Releases all resources used by the current instance.
        /// </summary>
        /// <remarks>Call this method when the instance is no longer needed to free managed resources
        /// promptly. After calling this method, the instance should not be used.</remarks>
        public void Dispose()
        {
            // Dispose the topic subscription to stop receiving messages and free associated resources.
            _topic?.Dispose();

            // Dispose the TagKeeper to free any resources it holds, such as connections to the distributed cache or timers for tag invalidation.
            _tagKeeper?.Dispose();

            // Dispose the local cache instances to free any resources or network connections.
            _memoryCache?.Dispose();

            // Dispose the distributed cache instance to free any resources or network connections.
            _distributedCache?.Dispose();

            // Dispose all semaphores in the _reqLocks dictionary to free resources and prevent memory leaks.
            foreach (var semaphore in _reqLocks.Values)
            {
                semaphore.Dispose();
            }

            // Clear the _reqLocks dictionary to remove references to the disposed semaphores and allow for garbage collection.
            _reqLocks.Clear();

            // Log that the instance has been disposed for better observability and troubleshooting.
            _logger.LogInformation("NCacheHybridCache instance has been disposed.");
        }

        #endregion Public API implementation
    }
}