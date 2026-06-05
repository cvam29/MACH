namespace Mach.Infrastructure.Adyen;

/// <summary>
/// Strongly-typed configuration for the Adyen payment gateway adapter, bound from the
/// <c>Adyen</c> configuration section.
/// </summary>
public sealed class AdyenOptions
{
    /// <summary>The configuration section name these options bind from.</summary>
    public const string SectionName = "Adyen";

    /// <summary>Adyen API key used to authenticate Checkout API requests.</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>The Adyen merchant account that owns the payment sessions.</summary>
    public string MerchantAccount { get; set; } = string.Empty;

    /// <summary>Shared secret used to validate webhook notification HMAC signatures.</summary>
    public string HmacKey { get; set; } = string.Empty;

    /// <summary>Adyen client key, surfaced to the Drop-in/Web Components front end.</summary>
    public string ClientKey { get; set; } = string.Empty;

    /// <summary>Target Adyen environment: <c>Test</c> or <c>Live</c>.</summary>
    public string Environment { get; set; } = "Test";

    /// <summary>The return URL Adyen redirects the shopper back to after Drop-in completes.</summary>
    public string ReturnUrl { get; set; } = string.Empty;

    /// <summary>
    /// Optional override for the Checkout API base URL (e.g. to point integration tests at a
    /// local WireMock server). When empty, the standard Adyen Test endpoint is used.
    /// </summary>
    public string? CheckoutEndpointOverride { get; set; }
}
