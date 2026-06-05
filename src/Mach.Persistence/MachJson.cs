using System.Text.Json;
using System.Text.Json.Serialization;

namespace Mach.Persistence;

/// <summary>Shared <see cref="JsonSerializerOptions"/> for outbox / projection payloads.</summary>
internal static class MachJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };
}
