namespace Mach.Auth.Functions;

/// <summary>
/// Configuration for the auth session cookies, bound from the <c>Auth:</c> configuration section.
/// </summary>
public sealed class AuthCookieOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Auth";

    /// <summary>Name of the access-token cookie.</summary>
    public string AccessCookieName { get; set; } = "mach_at";

    /// <summary>Name of the refresh-token cookie.</summary>
    public string RefreshCookieName { get; set; } = "mach_rt";

    /// <summary>
    /// Lifetime of the refresh cookie. commercetools refresh tokens are long-lived; the cookie
    /// is allowed to outlive the access token so a session can be transparently refreshed.
    /// </summary>
    public TimeSpan RefreshCookieLifetime { get; set; } = TimeSpan.FromDays(30);
}
