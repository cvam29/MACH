using System.Text.Json;
using System.Text.Json.Serialization;

namespace Mach.Infrastructure.Caching;

/// <summary>
/// Shared <see cref="JsonSerializerOptions"/> used by every cache store so that values written by
/// one provider (e.g. Redis) deserialize identically under another (e.g. in-memory) during tests
/// and provider swaps. Web defaults give camelCase + case-insensitive reads; we keep the payload
/// compact and tolerant of enum names.
/// </summary>
internal static class CacheSerialization
{
    /// <summary>The single, shared serializer configuration for all cache payloads.</summary>
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>Serialize <paramref name="value"/> to a UTF-8 JSON string using the shared options.</summary>
    public static string Serialize<T>(T value) => JsonSerializer.Serialize(value, Options);

    /// <summary>Deserialize <paramref name="json"/> to <typeparamref name="T"/> using the shared options.</summary>
    public static T? Deserialize<T>(string json) => JsonSerializer.Deserialize<T>(json, Options);
}
