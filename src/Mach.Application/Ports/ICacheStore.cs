namespace Mach.Application.Ports;

/// <summary>
/// Distributed cache abstraction for cache-aside read acceleration (catalog, content,
/// customer profile, delivery quotes). Backed by Azure Cache for Redis in the cloud and an
/// in-memory store for offline runs. SQL remains the source of truth for durable data
/// (outbox/inbox/projections); this is an ephemeral, invalidatable cache.
/// Implemented by <c>Mach.Infrastructure.Caching</c>.
/// </summary>
public interface ICacheStore
{
    /// <summary>Return the cached value for <paramref name="key"/>, or default if absent/expired.</summary>
    Task<T?> GetAsync<T>(string key, CancellationToken ct);

    /// <summary>Cache <paramref name="value"/> under <paramref name="key"/> for <paramref name="ttl"/>.</summary>
    Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken ct);

    /// <summary>
    /// Cache-aside helper: return the cached value, or invoke <paramref name="factory"/>,
    /// cache its result for <paramref name="ttl"/>, and return it.
    /// </summary>
    Task<T> GetOrSetAsync<T>(string key, Func<CancellationToken, Task<T>> factory, TimeSpan ttl, CancellationToken ct);

    /// <summary>Evict a single key (e.g. on a product/content change event).</summary>
    Task RemoveAsync(string key, CancellationToken ct);

    /// <summary>Evict every key under a logical prefix (e.g. <c>product:</c>) for bulk invalidation.</summary>
    Task RemoveByPrefixAsync(string prefix, CancellationToken ct);
}
