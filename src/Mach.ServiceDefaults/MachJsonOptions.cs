using System.Text.Json;
using System.Text.Json.Serialization;

namespace Mach.ServiceDefaults;

/// <summary>Shared JSON serialization options used across hosts and adapters.</summary>
public static class MachJsonOptions
{
    /// <summary>
    /// camelCase, case-insensitive, ignores nulls, serializes enums as strings.
    /// </summary>
    public static JsonSerializerOptions Default { get; } = Create();

    private static JsonSerializerOptions Create()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
        };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
