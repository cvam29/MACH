using Mach.Application.Ports;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace Mach.Infrastructure.Caching;

/// <summary>DI wiring for the cache adapter.</summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the configured <see cref="ICacheStore"/> implementation, selected by
    /// <c>Cache:Provider</c> (<see cref="CacheProvider.Redis"/> | <see cref="CacheProvider.InMemory"/>).
    /// </summary>
    /// <remarks>
    /// For <see cref="CacheProvider.Redis"/> a singleton <see cref="IConnectionMultiplexer"/> is
    /// registered that connects lazily on first resolution (so an unavailable Redis does not fail
    /// startup wiring). For <see cref="CacheProvider.InMemory"/> an <see cref="Microsoft.Extensions.Caching.Memory.IMemoryCache"/>
    /// is registered.
    /// </remarks>
    public static IServiceCollection AddCaching(this IServiceCollection services, IConfiguration config)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(config);

        var section = config.GetSection(CacheOptions.SectionName);
        services.AddOptions<CacheOptions>().Bind(section);

        // Resolve provider eagerly to decide which implementation to register.
        var provider = section.GetValue<CacheProvider>(nameof(CacheOptions.Provider));

        if (provider == CacheProvider.Redis)
        {
            services.AddSingleton<IConnectionMultiplexer>(_ =>
            {
                var options = section.Get<CacheOptions>() ?? new CacheOptions();
                if (string.IsNullOrWhiteSpace(options.ConnectionString))
                {
                    throw new InvalidOperationException(
                        "Redis cache provider requires Cache:ConnectionString.");
                }

                // Connect lazily at first resolution; Connect blocks until the handshake completes.
                return ConnectionMultiplexer.Connect(options.ConnectionString);
            });

            services.AddSingleton<ICacheStore>(sp =>
            {
                var options = sp.GetRequiredService<IOptions<CacheOptions>>().Value;
                return new RedisCacheStore(
                    sp.GetRequiredService<IConnectionMultiplexer>(),
                    options.InstanceName,
                    TimeSpan.FromSeconds(options.DefaultTtlSeconds));
            });
        }
        else
        {
            services.AddMemoryCache();
            services.AddSingleton<ICacheStore>(sp =>
            {
                var options = sp.GetRequiredService<IOptions<CacheOptions>>().Value;
                return new InMemoryCacheStore(
                    sp.GetRequiredService<Microsoft.Extensions.Caching.Memory.IMemoryCache>(),
                    TimeSpan.FromSeconds(options.DefaultTtlSeconds));
            });
        }

        return services;
    }
}
