using Mach.Domain.Auth;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace Mach.Auth.Functions;

/// <summary>
/// Builds and applies the session cookies (<c>mach_at</c> / <c>mach_rt</c>). Tokens live ONLY in
/// these cookies, never in the JSON body. All cookies are HttpOnly, Secure, SameSite=Lax, Path=/.
/// </summary>
public sealed class AuthCookieWriter(IOptions<AuthCookieOptions> options)
{
    private readonly AuthCookieOptions _options = options.Value;

    /// <summary>Builds the access-token cookie options for <paramref name="session"/>.</summary>
    public CookieOptions BuildAccessCookieOptions(CustomerSession session)
        => Build(session.ExpiresAt);

    /// <summary>Builds the refresh-token cookie options (a longer window than the access token).</summary>
    public CookieOptions BuildRefreshCookieOptions()
        => Build(DateTimeOffset.UtcNow.Add(_options.RefreshCookieLifetime));

    /// <summary>Builds the options used to expire/clear a cookie.</summary>
    public CookieOptions BuildClearCookieOptions()
        => Build(DateTimeOffset.UnixEpoch);

    /// <summary>
    /// Writes both session cookies onto <paramref name="response"/>. The access cookie is omitted
    /// when the session has no access token; the refresh cookie is omitted when there is no refresh
    /// token (e.g. an anonymous flow that returns only an access token).
    /// </summary>
    public void SetSessionCookies(HttpResponse response, CustomerSession session)
    {
        if (!string.IsNullOrEmpty(session.AccessToken))
        {
            response.Cookies.Append(
                _options.AccessCookieName, session.AccessToken, BuildAccessCookieOptions(session));
        }

        if (!string.IsNullOrEmpty(session.RefreshToken))
        {
            response.Cookies.Append(
                _options.RefreshCookieName, session.RefreshToken, BuildRefreshCookieOptions());
        }
    }

    /// <summary>Clears both session cookies on <paramref name="response"/>.</summary>
    public void ClearSessionCookies(HttpResponse response)
    {
        var clear = BuildClearCookieOptions();
        response.Cookies.Append(_options.AccessCookieName, string.Empty, clear);
        response.Cookies.Append(_options.RefreshCookieName, string.Empty, clear);
    }

    /// <summary>Reads the access-token cookie, or null when absent.</summary>
    public string? ReadAccessToken(HttpRequest request)
        => Read(request, _options.AccessCookieName);

    /// <summary>Reads the refresh-token cookie, or null when absent.</summary>
    public string? ReadRefreshToken(HttpRequest request)
        => Read(request, _options.RefreshCookieName);

    private static string? Read(HttpRequest request, string name)
        => request.Cookies.TryGetValue(name, out var value) && !string.IsNullOrEmpty(value)
            ? value
            : null;

    private static CookieOptions Build(DateTimeOffset expires) => new()
    {
        HttpOnly = true,
        Secure = true,
        SameSite = SameSiteMode.Lax,
        Path = "/",
        Expires = expires,
        IsEssential = true,
    };
}
