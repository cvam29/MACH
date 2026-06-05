using Mach.Infrastructure.Caching;
using Microsoft.Extensions.Caching.Memory;
using Shouldly;

namespace Mach.Infrastructure.Caching.Tests;

public sealed class InMemoryCacheStoreTests
{
    private static InMemoryCacheStore NewStore(TimeSpan? defaultTtl = null) =>
        new(new MemoryCache(new MemoryCacheOptions()), defaultTtl);

    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(5);

    private sealed record Product(int Id, string Name, decimal Price);

    [Fact]
    public async Task GetAsync_returns_default_on_miss()
    {
        var store = NewStore();

        var result = await store.GetAsync<Product>("product:missing", CancellationToken.None);

        result.ShouldBeNull();
    }

    [Fact]
    public async Task Set_then_Get_round_trips_typed_value()
    {
        var store = NewStore();
        var product = new Product(42, "Widget", 9.99m);

        await store.SetAsync("product:42", product, Ttl, CancellationToken.None);
        var result = await store.GetAsync<Product>("product:42", CancellationToken.None);

        result.ShouldBe(product);
    }

    [Fact]
    public async Task Get_returns_value_typed_as_primitive()
    {
        var store = NewStore();

        await store.SetAsync("count", 7, Ttl, CancellationToken.None);
        var result = await store.GetAsync<int>("count", CancellationToken.None);

        result.ShouldBe(7);
    }

    [Fact]
    public async Task GetOrSet_invokes_factory_once_then_caches()
    {
        var store = NewStore();
        var calls = 0;

        Task<Product> Factory(CancellationToken _)
        {
            Interlocked.Increment(ref calls);
            return Task.FromResult(new Product(1, "Cached", 1m));
        }

        var first = await store.GetOrSetAsync("p:1", Factory, Ttl, CancellationToken.None);
        var second = await store.GetOrSetAsync("p:1", Factory, Ttl, CancellationToken.None);

        first.ShouldBe(new Product(1, "Cached", 1m));
        second.ShouldBe(first);
        calls.ShouldBe(1);
    }

    [Fact]
    public async Task GetOrSet_does_not_cache_null_result()
    {
        var store = NewStore();
        var calls = 0;

        Task<Product?> Factory(CancellationToken _)
        {
            Interlocked.Increment(ref calls);
            return Task.FromResult<Product?>(null);
        }

        var first = await store.GetOrSetAsync("p:null", Factory, Ttl, CancellationToken.None);
        var second = await store.GetOrSetAsync("p:null", Factory, Ttl, CancellationToken.None);

        first.ShouldBeNull();
        second.ShouldBeNull();
        // No tombstone: factory runs again on the second call.
        calls.ShouldBe(2);
    }

    [Fact]
    public async Task Remove_evicts_single_key()
    {
        var store = NewStore();
        await store.SetAsync("k", "v", Ttl, CancellationToken.None);

        await store.RemoveAsync("k", CancellationToken.None);

        (await store.GetAsync<string>("k", CancellationToken.None)).ShouldBeNull();
    }

    [Fact]
    public async Task RemoveByPrefix_evicts_only_matching_keys()
    {
        var store = NewStore();
        await store.SetAsync("product:1", "a", Ttl, CancellationToken.None);
        await store.SetAsync("product:2", "b", Ttl, CancellationToken.None);
        await store.SetAsync("content:1", "c", Ttl, CancellationToken.None);

        await store.RemoveByPrefixAsync("product:", CancellationToken.None);

        (await store.GetAsync<string>("product:1", CancellationToken.None)).ShouldBeNull();
        (await store.GetAsync<string>("product:2", CancellationToken.None)).ShouldBeNull();
        (await store.GetAsync<string>("content:1", CancellationToken.None)).ShouldBe("c");
    }

    [Fact]
    public async Task Entry_expires_after_ttl()
    {
        var store = NewStore();
        await store.SetAsync("short", "v", TimeSpan.FromMilliseconds(50), CancellationToken.None);

        (await store.GetAsync<string>("short", CancellationToken.None)).ShouldBe("v");

        await Task.Delay(150);

        (await store.GetAsync<string>("short", CancellationToken.None)).ShouldBeNull();
    }

    [Fact]
    public async Task GetOrSet_uses_cached_value_after_set()
    {
        var store = NewStore();
        await store.SetAsync("k", new Product(9, "Pre", 3m), Ttl, CancellationToken.None);

        var calls = 0;
        var result = await store.GetOrSetAsync<Product>("k", _ =>
        {
            Interlocked.Increment(ref calls);
            return Task.FromResult(new Product(0, "FromFactory", 0m));
        }, Ttl, CancellationToken.None);

        result.ShouldBe(new Product(9, "Pre", 3m));
        calls.ShouldBe(0);
    }

    [Fact]
    public async Task Operations_throw_on_empty_key()
    {
        var store = NewStore();

        await Should.ThrowAsync<ArgumentException>(() => store.GetAsync<string>("", CancellationToken.None));
        await Should.ThrowAsync<ArgumentException>(() => store.SetAsync("", "v", Ttl, CancellationToken.None));
        await Should.ThrowAsync<ArgumentException>(() => store.RemoveAsync("", CancellationToken.None));
        await Should.ThrowAsync<ArgumentException>(() => store.RemoveByPrefixAsync("", CancellationToken.None));
    }
}
