namespace Mach.Infrastructure.Adyen;

/// <summary>Well-known Adyen Checkout API endpoints.</summary>
internal static class AdyenEndpoints
{
    /// <summary>Checkout API base URL for the Test environment.</summary>
    public const string CheckoutTestBaseUrl = "https://checkout-test.adyen.com/v71";

    /// <summary>Checkout API base URL for the Live environment (prefix-less; demo default).</summary>
    public const string CheckoutLiveBaseUrl = "https://checkout-live.adyen.com/v71";

    /// <summary>Relative path of the create-payment-session operation.</summary>
    public const string SessionsPath = "sessions";

    /// <summary>
    /// Resolves the Checkout base URL for the given options, honouring an explicit override.
    /// </summary>
    public static string ResolveCheckoutBaseUrl(AdyenOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.CheckoutEndpointOverride))
        {
            return options.CheckoutEndpointOverride.TrimEnd('/');
        }

        return string.Equals(options.Environment, "Live", StringComparison.OrdinalIgnoreCase)
            ? CheckoutLiveBaseUrl
            : CheckoutTestBaseUrl;
    }
}
