using System.Collections.Concurrent;
using Mach.Application.Ports;
using Microsoft.Extensions.Caching.Memory;

namespace Mach.Infrastructure.Caching;

/// <summary>
/// In-process <see cref="ICacheStore"/> backed by <see cref="IMemoryCache"/>, for offline/dev runs
/// where no Redis instance is available. Values are JSON round-tripped through
/// <see cref="CacheSerialization"/> so behaviour (including null/tombstone handling) matches the
/// Redis store.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="IMemoryCache"/> has no key-enumeration API, so to support
/// <see cref="RemoveByPrefixAsync"/> we track every live (prefixed) key in a concurrent set and
/// register an eviction callback that removes the key from the set when the entry expires or is
/// evicted. <see cref="RemoveByPrefixAsync"/> then scans that set in process.
/// </para>
/// <para>
/// <b>GetOrSet null-handling:</b> a factory result of <c>null</c> is NOT cached (no tombstone).
/// A subsequent call re-invokes the factory. This keeps the in-memory store consistent with the
/// Redis store and avoids pinning negative results; callers that need negative caching should cache
/// an explicit sentinel value.
/// </para>
/// </remarks>
public sealed class InMemoryCacheStore : ICacheStore
{
    private readonly IMemoryCache _cache;
    private readonly TimeSpan _defaultTtl;

    // Tracks live keys so RemoveByPrefixAsync can enumerate them (IMemoryCache cannot).
    private readonly ConcurrentDictionary<string, byte> _keys = new(StringComparer.Ordinal);

    /// <summary>Create a store over the supplied <paramref name="cache"/>.</summary>
    /// <param name="cache">The backing memory cache.</param>
    /// <param name="defaultTtl">
    /// TTL applied when a caller passes a non-positive <see cref="TimeSpan"/>. Defaults to 5 minutes.
    /// </param>
    public InMemoryCacheStore(IMemoryCache cache, TimeSpan? defaultTtl = null)
    {
        ArgumentNullException.ThrowIfNull(cache);
        _cache = cache;
        _defaultTtl = defaultTtl is { } t && t > TimeSpan.Zero ? t : TimeSpan.FromMinutes(5);
    }

    /// <inheritdoc />
    public Task<T?> GetAsync<T>(string key, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);
        ct.ThrowIfCancellationRequested();

        if (_cache.TryGetValue(key, out string? json) && json is not null)
        {
            return Task.FromResult(CacheSerialization.Deserialize<T>(json));
        }

        return Task.FromResult<T?>(default);
    }

    /// <inheritdoc />
    public Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);
        ct.ThrowIfCancellationRequested();

        var json = CacheSerialization.Serialize(value);
        Store(key, json, ttl);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task<T> GetOrSetAsync<T>(string key, Func<CancellationToken, Task<T>> factory, TimeSpan ttl, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);
        ArgumentNullException.ThrowIfNull(factory);
        ct.ThrowIfCancellationRequested();

        if (_cache.TryGetValue(key, out string? cached) && cached is not null)
        {
            return CacheSerialization.Deserialize<T>(cached)!;
        }

        var value = await factory(ct).ConfigureAwait(false);

        // Do not cache nulls (no tombstone) — see remarks.
        if (value is not null)
        {
            Store(key, CacheSerialization.Serialize(value), ttl);
        }

        return value;
    }

    /// <inheritdoc />
    public Task RemoveAsync(string key, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);
        ct.ThrowIfCancellationRequested();

        _cache.Remove(key);
        _keys.TryRemove(key, out _);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task RemoveByPrefixAsync(string prefix, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrEmpty(prefix);
        ct.ThrowIfCancellationRequested();

        foreach (var key in _keys.Keys)
        {
            if (key.StartsWith(prefix, StringComparison.Ordinal))
            {
                _cache.Remove(key);
                _keys.TryRemove(key, out _);
            }
        }

        return Task.CompletedTask;
    }

    private void Store(string key, string json, TimeSpan ttl)
    {
        var expiry = ttl > TimeSpan.Zero ? ttl : _defaultTtl;
        _keys[key] = 0;

        var entryOptions = new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = expiry,
        };
        // Keep the tracking set in sync when entries expire/are evicted out from under us.
        entryOptions.RegisterPostEvictionCallback(static (evictedKey, _, _, state) =>
        {
            ((ConcurrentDictionary<string, byte>)state!).TryRemove((string)evictedKey, out _);
        }, _keys);

        _cache.Set(key, json, entryOptions);
    }
}
