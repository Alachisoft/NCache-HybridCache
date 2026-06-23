using Alachisoft.NCache.Client;
using System.Net;

namespace NCache.OSS.Microsoft.Extensions.Caching.Hybrid
{
    /// <summary>
    /// Represents the configuration options for integrating NCache as a hybrid cache provider, specifying both local
    /// (L1) and distributed (L2) cache settings.
    /// </summary>
    /// <remarks>This configuration class is used to define the required cache names and server list for
    /// NCache hybrid caching scenarios. It enables the use of an in-process cache for fast local access and a
    /// replicated distributed cache for data consistency across multiple nodes. Additional options such as logging can
    /// be enabled for diagnostics. This class is typically used when configuring hybrid caching in applications that
    /// leverage NCache for both performance and scalability.</remarks>
    public class NCacheHybridCacheOptions
    {
        /// <summary>
        /// Specifies the configuration section name used for NCache hybrid cache settings.
        /// </summary>
        internal readonly static string SectionName = NCacheHybridCacheUtils.ConfigSectionName;

        /// <summary>
        /// Mandatory name of the NCache In-Proc cache to be used as local cache (L1 cache)
        /// </summary>
        public string? LocalCacheName { get; set; }

        /// <summary>
        /// Gets the duration for which items are retained in the local cache before expiration.
        /// </summary>
        /// <remarks>If set to <see langword="null"/>, local caching is disabled and items are not stored
        /// locally. Adjust this value to control cache freshness and memory usage.</remarks>
        public TimeSpan? LocalCacheExpiration { get; set; }

        /// <summary>
        /// Mandatory name of the NCache replicated cache to be used as distributed cache (L2 cache)
        /// </summary>
        public string? DistributedCacheName { get; set; }

        /// <summary>
        /// Gets the expiration time for items stored in the distributed cache.
        /// </summary>
        /// <remarks>If not set, the default expiration policy of the distributed cache provider is used.
        /// Use this property to control how long cached items remain available before they expire
        /// automatically.</remarks>
        public TimeSpan? DistributedCacheExpiration { get; set; }

        /// <summary>
        /// Gets/Sets list of servers provided by user
        /// </summary>
        public IList<ServerConfig> ServerList { get; set; } = new List<ServerConfig>();

        /// <summary>
        /// Initializes and returns a new instance of the cache connection options based on the current configuration.
        /// </summary>
        /// <remarks>This method aggregates various configuration properties into a single options object
        /// for establishing a cache connection. If an error occurs during initialization, the method returns <see
        /// langword="null"/>.</remarks>
        /// <returns>A <see cref="CacheConnectionOptions"/> instance initialized with the current settings; or <see
        /// langword="null"/> if initialization fails.</returns>
        internal CacheConnectionOptions? GetCacheConnectionOptions()
        {
            // Create a new instance of CacheConnectionOptions
            CacheConnectionOptions? cacheInitParams = null;
            try
            {
                // Check if the ServerList property is not null or empty
                if (ServerList != null && ServerList.Any())
                {
                    // Initialize cacheInitParams with a new instance of CacheConnectionOptions
                    cacheInitParams = new CacheConnectionOptions();

                    // Convert each ServerConfig in the ServerList to a ServerInfo object and assign the resulting list to the ServerList property of cacheInitParams
                    cacheInitParams.ServerList = ServerList.Select(s => s.ToServerInfo()).ToList();
                }
            }
            catch (Exception ex)
            {
                // In case of any exception during initialization set cacheInitParams to null
                cacheInitParams = null;

                // Throw the exception to be handled by the caller
                throw;
            }

            // Return the initialized cache connection options, or null if initialization failed
            return cacheInitParams;
        }

        /// <summary>
        /// Determines whether the current configuration is valid.
        /// </summary>
        /// <remarks>Validation fails if the local cache name, distributed cache name, or server list is
        /// missing or invalid. The method checks each server configuration for validity and returns the first
        /// encountered error.</remarks>
        /// <param name="err">When this method returns, contains an error message describing the first validation failure; otherwise, an
        /// empty string if the configuration is valid.</param>
        /// <returns>true if the configuration is valid; otherwise, false.</returns>
        internal bool isValid(out string err)
        {
            // Initialize the error message to an empty string
            err = string.Empty;

            // Validate that the LocalCacheName property is not null or empty
            if (string.IsNullOrEmpty(LocalCacheName))
            {
                err = $"{nameof(LocalCacheName)} is required.";
                return false;
            }

            // Validate that the DistributedCacheName property is not null or empty
            if (string.IsNullOrEmpty(DistributedCacheName))
            {
                err = $"{nameof(DistributedCacheName)} is required.";
                return false;
            }

            // Validate each server configuration in the ServerList
            if (ServerList != null && ServerList.Any())
            {
                foreach (var server in ServerList)
                {
                    if (!server.isValid(out err))
                        return false;
                }
            }

            return true;
        }
    }
    /// <summary>
    /// Represents the configuration settings required to connect to a server, including its IP address and port number.
    /// </summary>
    public record ServerConfig
    {
        /// <summary>
        /// Gets or sets the IP address associated with this instance.
        /// </summary>
        public string Ip { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the network port number used for connections.
        /// </summary>
        public int Port { get; set; } = 9800;

        /// <summary>
        /// Creates a new instance of the ServerInfo class using the current IP address and port.
        /// </summary>
        /// <returns>A ServerInfo object initialized with the current IP address and port.</returns>
        internal ServerInfo ToServerInfo() => new ServerInfo(Ip, Port);

        /// <summary>
        /// Determines whether the current IP address and port configuration is valid.
        /// </summary>
        /// <param name="err">When this method returns, contains an error message describing the validation failure if the configuration
        /// is invalid; otherwise, an empty string.</param>
        /// <returns>true if both the IP address and port are valid; otherwise, false.</returns>
        internal bool isValid(out string err)
        {
            err = string.Empty;
            if (!IPAddress.TryParse(Ip, out var ipAddress))
            {
                err = $"IP address '{Ip}' is invalid.";
                return false;
            }
            if (Port <= 0 || Port > 65535)
            {
                err = $"Port number '{Port}' must be between 1 and 65535.";
                return false;
            }
            return true;
        }
    }
}
