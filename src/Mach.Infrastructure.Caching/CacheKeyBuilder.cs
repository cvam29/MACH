namespace Mach.Infrastructure.Caching;

/// <summary>
/// Builds physical Redis keys from a logical key and the configured instance namespace, so the
/// prefixing rule lives in one place and is unit-testable without a Redis connection.
/// </summary>
internal static class CacheKeyBuilder
{
    /// <summary>Physical key: <c>{instanceName}:{key}</c>.</summary>
    public static string Build(string instanceName, string key) => $"{instanceName}:{key}";

    /// <summary><c>SCAN MATCH</c> glob covering every key under a logical prefix: <c>{instanceName}:{prefix}*</c>.</summary>
    public static string Pattern(string instanceName, string prefix) => $"{instanceName}:{prefix}*";
}
