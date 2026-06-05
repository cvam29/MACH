using System.Text.Json.Serialization;

namespace Mach.Infrastructure.Commercetools;

/// <summary>
/// The OAuth2 token response returned by the commercetools auth server for the password,
/// anonymous-session, refresh and client-credentials grants.
/// </summary>
internal sealed class OAuthTokenResponse
{
    [JsonPropertyName("access_token")]
    public string AccessToken { get; set; } = string.Empty;

    [JsonPropertyName("refresh_token")]
    public string? RefreshToken { get; set; }

    [JsonPropertyName("token_type")]
    public string? TokenType { get; set; }

    [JsonPropertyName("expires_in")]
    public long ExpiresIn { get; set; }

    [JsonPropertyName("scope")]
    public string? Scope { get; set; }

    /// <summary>
    /// Extracts the <c>anonymous_id:&lt;value&gt;</c> token embedded in the granted scope of an
    /// anonymous-session response, when present.
    /// </summary>
    public string? AnonymousId =>
        Scope?
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault(s => s.Contains("anonymous_id:", StringComparison.OrdinalIgnoreCase))?
            .Split(':', 2)
            .ElementAtOrDefault(1);
}
