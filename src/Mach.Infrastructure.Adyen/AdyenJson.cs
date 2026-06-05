using System.Text.Json;

using AdyenModels = Adyen.Checkout.Models;

namespace Mach.Infrastructure.Adyen;

/// <summary>
/// Shared <see cref="JsonSerializerOptions"/> configured with the Adyen Checkout model
/// converters, used for serializing the <c>/sessions</c> request and deserializing its response.
/// </summary>
internal static class AdyenJson
{
    /// <summary>Options wired with the Adyen-generated Checkout converters.</summary>
    public static readonly JsonSerializerOptions Checkout = CreateCheckoutOptions();

    /// <summary>Case-insensitive options for parsing inbound webhook notification JSON.</summary>
    public static readonly JsonSerializerOptions Webhook = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private static JsonSerializerOptions CreateCheckoutOptions()
    {
        var options = new JsonSerializerOptions
        {
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
            PropertyNameCaseInsensitive = true,
        };

        options.Converters.Add(new AdyenModels.CreateCheckoutSessionRequestJsonConverter());
        options.Converters.Add(new AdyenModels.CreateCheckoutSessionResponseJsonConverter());
        options.Converters.Add(new AdyenModels.AmountJsonConverter());

        return options;
    }
}
