namespace NCache.OSS.Microsoft.Extensions.Caching.Hybrid
{
    /// <summary>
    /// Represents a cache item for hybrid caching scenarios that stores a value along with associated tags and a creation timestamp.
    /// </summary>
    /// <remarks>This class is intended for use in hybrid caching scenarios where items may be grouped or
    /// invalidated by tags. The creation timestamp can be used to determine the age of the cache entry.</remarks>
    [Serializable]
    internal sealed class NCacheHybridCacheMessage
    {
        /// <summary>
        /// Gets the unique identifier associated with the current instance.
        /// </summary>
        public string Identifier { get; set; } = string.Empty;

        /// <summary>
        /// Gets the name of the entity (tag/key) associated with this instance.
        /// </summary>
        public IEnumerable<string> Entities { get; set; } = Array.Empty<string>();

        /// <summary>
        /// Gets the type of the hybrid cache message.
        /// </summary>
        public HybridCacheMessageType Type { get; set; } = HybridCacheMessageType.None;

        /// <summary>
        /// Initializes a new instance of the NCacheHybridMessage class with default values for serialization.
        /// </summary>
        internal NCacheHybridCacheMessage()
        {
            Identifier = string.Empty;
            Entities = Array.Empty<string>();
            Type = HybridCacheMessageType.None;
        }

        /// <summary>
        /// Initializes a new instance of the NCacheHybridMessage class with the given parameters.
        /// </summary>
        /// <param name="identifier">The unique identifier for the message. Cannot be null.</param>
        /// <param name="entity">The name of the entity associated with the message. Cannot be null.</param>
        /// <param name="type">The type of the hybrid cache message.</param>
        internal NCacheHybridCacheMessage(string identifier, string entity, HybridCacheMessageType type)
        {
            Identifier = identifier;
            Entities = new string[] { entity };
            Type = type;
        }

        /// <summary>
        /// Initializes a new instance of the NCacheHybridCacheMessage class with the specified identifier, entities,
        /// and message type.
        /// </summary>
        /// <param name="identifier">The unique identifier for the cache message. Cannot be null.</param>
        /// <param name="entities">A collection of entity names associated with the cache message. Cannot be null.</param>
        /// <param name="type">The type of the hybrid cache message to be created.</param>
        internal NCacheHybridCacheMessage(string identifier, IEnumerable<string> entities, HybridCacheMessageType type)
        {
            Identifier = identifier;
            Entities = entities;
            Type = type;
        }

        /// <summary>
        /// Returns a string that represents the current object, including its identifier, entity, and type.
        /// </summary>
        /// <returns>A string containing the values of the Identifier, Entity, and Type properties.</returns>
        public override string ToString()
        {
            return $@"{Identifier}{Environment.NewLine}{string.Join(", ", Entities)}{Environment.NewLine}{Type}";
        }
    }

    /// <summary>
    /// Specifies the types of messages that can be sent within the hybrid cache system.
    /// </summary>
    /// <remarks>This enumeration is used internally to distinguish between different cache operations, such
    /// as updating, removing, or tagging cache entries, as well as handling wildcard operations.</remarks>
    internal enum HybridCacheMessageType
    {
        /// <summary>
        /// Specifies that no options are set.
        /// </summary>
        None = 0,

        /// <summary>
        /// Specifies an update operation is performed on a cache item
        /// </summary>
        Update = 1,

        /// <summary>
        /// Specifies that a item has been removed from cache
        /// </summary>
        Remove = 2,

        /// <summary>
        /// Specifies that a tag has been invalidated
        /// </summary>
        Tag = 3,

        /// <summary>
        /// Specifies that wildcard has been used to invalidate all tags
        /// </summary>
        WildCard = 4
    }
}
