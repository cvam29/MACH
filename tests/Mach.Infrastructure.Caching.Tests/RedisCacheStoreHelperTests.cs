using Mach.Infrastructure.Caching;
using Shouldly;

namespace Mach.Infrastructure.Caching.Tests;

/// <summary>
/// Exercises <see cref="RedisCacheStore"/> behaviour that does NOT require a live server: key
/// prefixing (via the shared <see cref="CacheKeyBuilder"/>) and construction guards. No socket is
/// ever opened.
/// </summary>
public sealed class RedisCacheStoreHelperTests
{
    [Fact]
    public void BuildKey_prefixes_with_instance_name()
    {
        CacheKeyBuilder.Build("Mach", "product:42").ShouldBe("Mach:product:42");
    }

    [Fact]
    public void BuildPrefixPattern_appends_glob_wildcard()
    {
        CacheKeyBuilder.Pattern("Mach", "product:").ShouldBe("Mach:product:*");
    }

    [Fact]
    public void Different_instance_names_produce_isolated_keys()
    {
        CacheKeyBuilder.Build("A", "k").ShouldBe("A:k");
        CacheKeyBuilder.Build("B", "k").ShouldBe("B:k");
    }

    [Fact]
    public void Constructor_rejects_null_connection()
    {
        Should.Throw<ArgumentNullException>(() => new RedisCacheStore(null!, "Mach", null));
    }
}
