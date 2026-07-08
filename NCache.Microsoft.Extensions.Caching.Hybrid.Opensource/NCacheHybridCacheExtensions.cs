using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace NCache.OSS.Caching.Hybrid
{
    /// <summary>
    /// Provides extension methods for registering and configuring the NCache-based hybrid cache implementation with an
    /// application's dependency injection container.
    /// </summary>
    /// <remarks>These methods enable hybrid caching using NCache by adding the necessary services and
    /// configuration to the service collection. Call the appropriate method during application startup to integrate
    /// NCache hybrid caching. The hybrid cache is registered as a singleton service. Use with caution, as the
    /// HybridCache is experimental and may be subject to breaking changes.</remarks>
    public static class NCacheHybridCacheExtensions
    {
        /// <summary>
        /// Adds and configures the NCache-based hybrid cache implementation to the service collection.
        /// </summary>
        /// <remarks>Registers the NCache hybrid cache as a singleton service. Call this method during
        /// application startup to enable hybrid caching using NCache.</remarks>
        /// <param name="services">The service collection to which the NCache hybrid cache services are added. Cannot be null.</param>
        /// <param name="configuration">A delegate to configure the NCache hybrid cache options. Cannot be null.</param>
        /// <returns>The same service collection instance, enabling method chaining.</returns>
        /// <exception cref="ArgumentNullException">Thrown if either the services or configuration parameter is null.</exception>
        public static IServiceCollection AddNCacheHybridCache(this IServiceCollection services, Action<NCacheHybridCacheOptions> configuration)
        {
            // Validate service collection and throw ArgumentNullException if null
            if (services == null) throw new ArgumentNullException(nameof(services));

            // Validate configuration action and throw ArgumentNullException if null
            if (configuration == null) throw new ArgumentNullException(nameof(configuration));

            // Configure the NCache hybrid cache options using the provided configuration action
            services.Configure(configuration);

#pragma warning disable EXTEXP0018      // HybridCache is experimental and may be subject to breaking changes. Use with caution.

            // Register the NCache hybrid cache as a singleton service using logger and options from the service provider.
            services.AddSingleton<HybridCache>(sp =>
            {
                var options = sp.GetRequiredService<IOptions<NCacheHybridCacheOptions>>();
                var logger = sp.GetService<global::Microsoft.Extensions.Logging.ILogger<NCacheHybridCache>>();
                return new NCacheHybridCache(options, logger);
            });

#pragma warning restore EXTEXP0018      // HybridCache is experimental and may be subject to breaking changes. Use with caution.

            // Return the service collection to allow for method chaining
            return services;
        }

        /// <summary>
        /// Adds and configures the NCache-based hybrid cache implementation to the application's dependency injection
        /// container.
        /// </summary>
        /// <remarks>This method registers the NCache hybrid cache as a singleton service and binds
        /// configuration settings from the provided configuration source. Call this method during application startup
        /// to enable hybrid caching with NCache.</remarks>
        /// <param name="services">The service collection to which the hybrid cache services are added. Cannot be null.</param>
        /// <param name="configuration">The application configuration containing NCache hybrid cache settings. Cannot be null.</param>
        /// <returns>The same service collection instance, enabling method chaining.</returns>
        /// <exception cref="ArgumentNullException">Thrown if either the services or configuration parameter is null.</exception>
        public static IServiceCollection AddNCacheHybridCache(this IServiceCollection services, IConfiguration configuration)
        {
            // Validate service collection and throw ArgumentNullException if null
            if (services == null) throw new ArgumentNullException(nameof(services));

            // Validate configuration action and throw ArgumentNullException if null
            if (configuration == null) throw new ArgumentNullException(nameof(configuration));

            // Attempt to bind the NCache hybrid cache configuration section from the provided configuration source
            var section = configuration.GetSection(NCacheHybridCacheOptions.SectionName);

            // If the configuration section exists and has child settings, bind it to the NCacheHybridCacheConfig options; otherwise, bind the entire configuration
            if (section != null && section.GetChildren().Any())
                services.Configure<NCacheHybridCacheOptions>(section);
            else services.Configure<NCacheHybridCacheOptions>(configuration);

#pragma warning disable EXTEXP0018      // HybridCache is experimental and may be subject to breaking changes. Use with caution.

            // Register the NCache hybrid cache as a singleton service using logger and options from the service provider.
            services.AddSingleton<HybridCache>(sp =>
            {
                var options = sp.GetRequiredService<IOptions<NCacheHybridCacheOptions>>();
                var logger = sp.GetService<global::Microsoft.Extensions.Logging.ILogger<NCacheHybridCache>>();
                return new NCacheHybridCache(options, logger);
            });

#pragma warning restore EXTEXP0018      // HybridCache is experimental and may be subject to breaking changes. Use with caution.

            // Return the service collection to allow for method chaining
            return services;
        }
    }
}
