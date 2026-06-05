using Mach.Application.Ports;
using Mach.Infrastructure.Caching;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Shouldly;
using StackExchange.Redis;

namespace Mach.Infrastructure.Caching.Tests;

public sealed class AddCachingTests
{
    private static IConfiguration Config(Dictionary<string, string?> values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values).Build();

    [Fact]
    public void InMemory_provider_resolves_InMemoryCacheStore()
    {
        var config = Config(new() { ["Cache:Provider"] = "InMemory" });
        var services = new ServiceCollection();

        services.AddCaching(config);
        using var sp = services.BuildServiceProvider();

        sp.GetRequiredService<ICacheStore>().ShouldBeOfType<InMemoryCacheStore>();
        sp.GetService<IMemoryCache>().ShouldNotBeNull();
    }

    [Fact]
    public void Default_provider_when_unset_is_InMemory()
    {
        var config = Config(new());
        var services = new ServiceCollection();

        services.AddCaching(config);
        using var sp = services.BuildServiceProvider();

        sp.GetRequiredService<ICacheStore>().ShouldBeOfType<InMemoryCacheStore>();
    }

    [Fact]
    public void Redis_provider_resolves_RedisCacheStore_without_connecting()
    {
        // Register Redis but never resolve ICacheStore/IConnectionMultiplexer, so no connection is attempted.
        var config = Config(new()
        {
            ["Cache:Provider"] = "Redis",
            ["Cache:ConnectionString"] = "localhost:6379",
            ["Cache:InstanceName"] = "Mach",
        });
        var services = new ServiceCollection();

        services.AddCaching(config);

        services.ShouldContain(d => d.ServiceType == typeof(ICacheStore));
        services.ShouldContain(d => d.ServiceType == typeof(IConnectionMultiplexer));
    }

    [Fact]
    public void Options_bind_from_Cache_section()
    {
        var config = Config(new()
        {
            ["Cache:Provider"] = "Redis",
            ["Cache:ConnectionString"] = "localhost:6379",
            ["Cache:InstanceName"] = "Shop",
            ["Cache:DefaultTtlSeconds"] = "120",
        });
        var services = new ServiceCollection();

        services.AddCaching(config);
        using var sp = services.BuildServiceProvider();

        var options = sp.GetRequiredService<IOptions<CacheOptions>>().Value;
        options.Provider.ShouldBe(CacheProvider.Redis);
        options.InstanceName.ShouldBe("Shop");
        options.DefaultTtlSeconds.ShouldBe(120);
        options.ConnectionString.ShouldBe("localhost:6379");
    }

    [Fact]
    public void CacheOptions_defaults_are_sensible()
    {
        var options = new CacheOptions();

        options.Provider.ShouldBe(CacheProvider.InMemory);
        options.InstanceName.ShouldBe("Mach");
        options.DefaultTtlSeconds.ShouldBe(300);
        options.ConnectionString.ShouldBeNull();
    }
}
