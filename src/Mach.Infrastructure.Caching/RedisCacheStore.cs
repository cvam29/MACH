using Mach.Application.Ports;
using StackExchange.Redis;

namespace Mach.Infrastructure.Caching;

/// <summary>
/// Distributed <see cref="ICacheStore"/> backed by Azure Cache for Redis via StackExchange.Redis.
/// Values are JSON-serialized with the shared <see cref="CacheSerialization"/> options and stored
/// under a configurable instance prefix so multiple apps/environments can share one Redis instance.
/// </summary>
/// <remarks>
/// <para>
/// <b>Key prefixing:</b> every logical key is stored as <c>{InstanceName}:{key}</c>
/// (e.g. instance <c>Mach</c> + key <c>product:42</c> → <c>Mach:product:42</c>). The prefix is
/// applied transparently; callers always use the logical key.
/// </para>
/// <para>
/// <b>GetOrSet null-handling:</b> a factory result of <c>null</c> is NOT cached (no tombstone). A
/// subsequent call re-invokes the factory. Callers needing negative caching should cache an explicit
/// sentinel.
/// </para>
/// <para>
/// <b>RemoveByPrefixAsync cost:</b> Redis has no native "delete by prefix" command. We enumerate
/// matching keys with <see cref="IServer.Keys(int, RedisValue, int, long, int, CommandFlags)"/>,
/// which issues <c>SCAN MATCH {InstanceName}:{prefix}*</c> against each connected, non-replica
/// endpoint. SCAN is O(N) over the entire keyspace (cursor-based, server-side glob filtering) and on
/// a large database can be expensive and span many round-trips; it is intended for occasional bulk
/// invalidation (e.g. a catalog reindex), not hot-path use. We delete matches in batches.
/// </para>
/// </remarks>
public sealed class RedisCacheStore : ICacheStore
{
    private const int ScanPageSize = 250;
    private const int DeleteBatchSize = 500;

    private readonly IConnectionMultiplexer _connection;
    private readonly string _instanceName;
    private readonly TimeSpan _defaultTtl;

    /// <summary>Create a store over the supplied connection.</summary>
    /// <param name="connection">A connected StackExchange.Redis multiplexer.</param>
    /// <param name="instanceName">Key prefix namespace (e.g. <c>Mach</c>). Required.</param>
    /// <param name="defaultTtl">TTL applied when a caller passes a non-positive <see cref="TimeSpan"/>. Defaults to 5 minutes.</param>
    public RedisCacheStore(IConnectionMultiplexer connection, string instanceName, TimeSpan? defaultTtl = null)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentException.ThrowIfNullOrWhiteSpace(instanceName);
        _connection = connection;
        _instanceName = instanceName;
        _defaultTtl = defaultTtl is { } t && t > TimeSpan.Zero ? t : TimeSpan.FromMinutes(5);
    }

    /// <summary>Build the physical Redis key for a logical <paramref name="key"/> (<c>{InstanceName}:{key}</c>).</summary>
    internal string BuildKey(string key) => CacheKeyBuilder.Build(_instanceName, key);

    /// <summary>The <c>SCAN MATCH</c> glob pattern covering every key under a logical prefix.</summary>
    internal string BuildPrefixPattern(string prefix) => CacheKeyBuilder.Pattern(_instanceName, prefix);

    private IDatabase Db => _connection.GetDatabase();

    /// <inheritdoc />
    public async Task<T?> GetAsync<T>(string key, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);
        ct.ThrowIfCancellationRequested();

        RedisValue value = await Db.StringGetAsync(BuildKey(key)).ConfigureAwait(false);
        return value.IsNullOrEmpty ? default : CacheSerialization.Deserialize<T>(value!);
    }

    /// <inheritdoc />
    public async Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);
        ct.ThrowIfCancellationRequested();

        var json = CacheSerialization.Serialize(value);
        await Db.StringSetAsync(BuildKey(key), json, ResolveTtl(ttl)).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<T> GetOrSetAsync<T>(string key, Func<CancellationToken, Task<T>> factory, TimeSpan ttl, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);
        ArgumentNullException.ThrowIfNull(factory);
        ct.ThrowIfCancellationRequested();

        RedisValue existing = await Db.StringGetAsync(BuildKey(key)).ConfigureAwait(false);
        if (!existing.IsNullOrEmpty)
        {
            return CacheSerialization.Deserialize<T>(existing!)!;
        }

        var value = await factory(ct).ConfigureAwait(false);

        // Do not cache nulls (no tombstone) — see remarks.
        if (value is not null)
        {
            await Db.StringSetAsync(BuildKey(key), CacheSerialization.Serialize(value), ResolveTtl(ttl)).ConfigureAwait(false);
        }

        return value;
    }

    /// <inheritdoc />
    public Task RemoveAsync(string key, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);
        ct.ThrowIfCancellationRequested();

        return Db.KeyDeleteAsync(BuildKey(key));
    }

    /// <inheritdoc />
    public async Task RemoveByPrefixAsync(string prefix, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrEmpty(prefix);
        ct.ThrowIfCancellationRequested();

        var pattern = BuildPrefixPattern(prefix);
        var db = Db;
        var batch = new List<RedisKey>(DeleteBatchSize);

        // SCAN each master endpoint (replicas mirror the same keyspace). O(N) over the keyspace.
        foreach (var endpoint in _connection.GetEndPoints())
        {
            var server = _connection.GetServer(endpoint);
            if (!server.IsConnected || server.IsReplica)
            {
                continue;
            }

            await foreach (var redisKey in server.KeysAsync(database: db.Database, pattern: pattern, pageSize: ScanPageSize).WithCancellation(ct).ConfigureAwait(false))
            {
                batch.Add(redisKey);
                if (batch.Count >= DeleteBatchSize)
                {
                    await db.KeyDeleteAsync(batch.ToArray()).ConfigureAwait(false);
                    batch.Clear();
                }
            }
        }

        if (batch.Count > 0)
        {
            await db.KeyDeleteAsync(batch.ToArray()).ConfigureAwait(false);
        }
    }

    private TimeSpan ResolveTtl(TimeSpan ttl) => ttl > TimeSpan.Zero ? ttl : _defaultTtl;
}
