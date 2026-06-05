namespace Mach.Infrastructure.Commercetools;

/// <summary>
/// Strongly-typed configuration for the commercetools adapter, bound from the
/// <c>Commercetools:</c> configuration section.
/// </summary>
public sealed class CommercetoolsOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Commercetools";

    /// <summary>The commercetools project key (tenant).</summary>
    public string ProjectKey { get; set; } = string.Empty;

    /// <summary>OAuth2 client id for the service (machine-to-machine) client.</summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>OAuth2 client secret for the service client.</summary>
    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>OAuth2 scopes requested for the service client (space-separated).</summary>
    public string ScopeList { get; set; } = string.Empty;

    /// <summary>Base URL of the commercetools HTTP API (e.g. https://api.{region}.commercetools.com).</summary>
    public string ApiUrl { get; set; } = string.Empty;

    /// <summary>Base URL of the commercetools auth/token endpoint (e.g. https://auth.{region}.commercetools.com).</summary>
    public string AuthUrl { get; set; } = string.Empty;

    /// <summary>Optional region hint (informational; URLs above are authoritative).</summary>
    public string? Region { get; set; }

    /// <summary>
    /// The locale used when projecting commercetools <c>LocalizedString</c> values down to a single
    /// string. Falls back to the first available translation when the locale is missing.
    /// </summary>
    public string DefaultLocale { get; set; } = "en";

    /// <summary>
    /// The fully-qualified OAuth2 token endpoint. commercetools exposes it at
    /// <c>{AuthUrl}/oauth/token</c>.
    /// </summary>
    public string TokenEndpoint =>
        $"{AuthUrl.TrimEnd('/')}/oauth/token";

    /// <summary>
    /// The OAuth2 token endpoint used to mint customer-scoped (password) tokens. commercetools
    /// scopes these per project: <c>{AuthUrl}/oauth/{ProjectKey}/customers/token</c>.
    /// </summary>
    public string CustomerTokenEndpoint =>
        $"{AuthUrl.TrimEnd('/')}/oauth/{ProjectKey}/customers/token";

    /// <summary>
    /// The OAuth2 token endpoint used to mint anonymous-session tokens:
    /// <c>{AuthUrl}/oauth/{ProjectKey}/anonymous/token</c>.
    /// </summary>
    public string AnonymousTokenEndpoint =>
        $"{AuthUrl.TrimEnd('/')}/oauth/{ProjectKey}/anonymous/token";
}
