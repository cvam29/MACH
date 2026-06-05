using System.Text.Json;
using System.Text.Json.Serialization;

namespace Mach.Infrastructure.Contentstack;

/// <summary>
/// Envelope returned by a CDA entries query: <c>GET /v3/content_types/{uid}/entries</c>.
/// Entry fields are kept as raw <see cref="JsonElement"/> so the adapter can map them.
/// </summary>
internal sealed class EntriesResponse
{
    [JsonPropertyName("entries")]
    public List<JsonElement> Entries { get; set; } = [];
}
