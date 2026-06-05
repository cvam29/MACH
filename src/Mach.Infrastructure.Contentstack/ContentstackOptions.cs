namespace Mach.Infrastructure.Contentstack;

/// <summary>
/// Strongly-typed configuration for the Contentstack Content Delivery API (CDA) adapter,
/// bound from the <c>Contentstack:</c> configuration section.
/// </summary>
public sealed class ContentstackOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Contentstack";

    /// <summary>Stack API key, sent as the <c>api_key</c> header.</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Delivery token, sent as the <c>access_token</c> header.</summary>
    public string DeliveryToken { get; set; } = string.Empty;

    /// <summary>Delivery environment (e.g. <c>development</c>), sent on every query.</summary>
    public string Environment { get; set; } = string.Empty;

    /// <summary>Content locale used for delivery queries.</summary>
    public string Locale { get; set; } = "en-us";

    /// <summary>
    /// Region shortcut used to derive the CDA host when <see cref="BaseUrl"/> is not set.
    /// Known values: <c>us</c> (default), <c>eu</c>, <c>azure-na</c>, <c>azure-eu</c>, <c>gcp-na</c>, <c>gcp-eu</c>.
    /// </summary>
    public string? Region { get; set; }

    /// <summary>
    /// Explicit CDA base URL. When set it wins over <see cref="Region"/>.
    /// Defaults to the US delivery host when neither is provided.
    /// </summary>
    public string? BaseUrl { get; set; }

    /// <summary>True when the required credentials and environment are present.</summary>
    public bool IsValid =>
        !string.IsNullOrWhiteSpace(ApiKey)
        && !string.IsNullOrWhiteSpace(DeliveryToken)
        && !string.IsNullOrWhiteSpace(Environment);

    /// <summary>Resolves the effective CDA base URL from <see cref="BaseUrl"/> or <see cref="Region"/>.</summary>
    public string ResolveBaseUrl()
    {
        if (!string.IsNullOrWhiteSpace(BaseUrl))
        {
            return BaseUrl.TrimEnd('/');
        }

        return Region?.Trim().ToLowerInvariant() switch
        {
            "eu" => "https://eu-cdn.contentstack.com",
            "azure-na" => "https://azure-na-cdn.contentstack.com",
            "azure-eu" => "https://azure-eu-cdn.contentstack.com",
            "gcp-na" => "https://gcp-na-cdn.contentstack.com",
            "gcp-eu" => "https://gcp-eu-cdn.contentstack.com",
            _ => "https://cdn.contentstack.io",
        };
    }
}
