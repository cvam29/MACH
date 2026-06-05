using System.Text.Json;

namespace Mach.Infrastructure.Adyen;

/// <summary>
/// Shared <see cref="JsonSerializerOptions"/> for parsing inbound Adyen webhook notification JSON.
/// Session creation no longer needs bespoke serializer options: the Adyen .NET SDK's checkout
/// service serializes the <c>/sessions</c> request and deserializes its response internally.
/// </summary>
internal static class AdyenJson
{
    /// <summary>Case-insensitive options for parsing inbound webhook notification JSON.</summary>
    public static readonly JsonSerializerOptions Webhook = new()
    {
        PropertyNameCaseInsensitive = true,
    };
}
