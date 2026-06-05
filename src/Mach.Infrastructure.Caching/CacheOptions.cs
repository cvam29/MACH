namespace Mach.Infrastructure.Caching;

/// <summary>Selects which <see cref="Application.Ports.ICacheStore"/> implementation is wired up.</summary>
public enum CacheProvider
{
    /// <summary>In-process cache backed by <see cref="Microsoft.Extensions.Caching.Memory.IMemoryCache"/>. Default for offline dev runs.</summary>
    InMemory,

    /// <summary>Distributed cache backed by Azure Cache for Redis (StackExchange.Redis).</summary>
    Redis,
}

/// <summary>
/// Configuration for the cache adapter, bound from the <c>Cache:</c> section.
/// </summary>
public sealed class CacheOptions
{
    /// <summary>The configuration section name these options bind to.</summary>
    public const string SectionName = "Cache";

    /// <summary>Which cache implementation to use. Defaults to <see cref="CacheProvider.InMemory"/>.</summary>
    public CacheProvider Provider { get; set; } = CacheProvider.InMemory;

    /// <summary>
    /// StackExchange.Redis connection string (e.g. <c>my-cache.redis.cache.windows.net:6380,password=...,ssl=True</c>).
    /// Required when <see cref="Provider"/> is <see cref="CacheProvider.Redis"/>.
    /// </summary>
    public string? ConnectionString { get; set; }

    /// <summary>
    /// Logical namespace used to prefix every cache key (so multiple apps/environments can share a
    /// Redis instance without colliding). Keys are stored as <c>{InstanceName}:{key}</c>.
    /// Defaults to <c>Mach</c>.
    /// </summary>
    public string InstanceName { get; set; } = "Mach";

    /// <summary>Default time-to-live, in seconds, applied when a caller does not specify one. Defaults to 300s.</summary>
    public int DefaultTtlSeconds { get; set; } = 300;
}
