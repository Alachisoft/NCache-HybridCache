using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace NCache.OSS.Caching.Hybrid
{
    /// <summary>
    /// Represents a cache item for hybrid caching scenarios that stores a value along with associated tags and a creation timestamp.
    /// </summary>
    /// <remarks>This class is intended for use in hybrid caching scenarios where items may be tags. 
    /// The creation timestamp can be used to determine the age of the cache entry.</remarks>
    [Serializable]
    internal sealed class NCacheHybridCacheItem
    {
        private static readonly JsonSerializer Serializer =
            JsonSerializer.Create(new JsonSerializerSettings
            {
                ObjectCreationHandling = ObjectCreationHandling.Replace,
                PreserveReferencesHandling = PreserveReferencesHandling.Objects
            });
        
        /// <summary>
        /// Represents the value that needs to be cached
        /// </summary>
        public object? Value;

        /// <summary>
        /// Specifies the type associated with the current instance.
        /// </summary>
        public string? TypeName;

        /// <summary>
        /// Gets the collection of tags associated with this cached item
        /// </summary>
        public IEnumerable<string> Tags = new List<string>();

        /// <summary>
        /// Timestamp indicating when this cache item was created, stored as Unix time in milliseconds
        /// </summary>
        public long CreatedTimeStamp;

        /// <summary>
        /// Initializes a new instance of the HybridCacheItem class with default values.
        /// </summary>
        /// <remarks>This constructor is used for serialization.</remarks>
        internal NCacheHybridCacheItem()
        {
            Value = null;
            TypeName = null;
            Tags = new List<string>();
            CreatedTimeStamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        }

        /// <summary>
        /// Initializes a new instance of the HybridCacheItem class with the specified value and associated tags.
        /// </summary>
        /// <param name="value">The value to be stored in the cache item. This can be any object to be cached.</param>
        /// <param name="tags">A collection of tags associated with the cache item. Tags can be used to group or identify related cache
        /// entries.</param>
        internal NCacheHybridCacheItem(object value, IEnumerable<string>? tags)
        {
            Value = value;
            TypeName = value?.GetType().AssemblyQualifiedName;
            Tags = tags ?? new List<string>();
            CreatedTimeStamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        }

        /// <summary>
        /// Retrieves the stored value cast to the specified type.
        /// </summary>
        /// <typeparam name="T">The type to which the stored value is cast and returned.</typeparam>
        /// <returns>The stored value cast to type T.</returns>
        /// <exception cref="InvalidCastException">Thrown if the stored value cannot be cast to the specified type T.</exception>
        internal T GetValue<T>()
        {
            string expectedType =
                typeof(T).AssemblyQualifiedName ?? typeof(T).FullName!;

            // Check if the stored type matches the requested type before casting
            if (TypeName != expectedType)
            {
                throw new InvalidCastException(
                    $"Cannot cast value of type {TypeName} to {expectedType}.");
            }

            // Enumerable converts into JArray, explicit conversion in case value is JArray
            if (Value is JArray jArray)
            {
                return jArray.ToObject<T>(Serializer)!;
            }

            // Return the stored value cast to the requested type T
            return (T)Value!;
        }
    }
}