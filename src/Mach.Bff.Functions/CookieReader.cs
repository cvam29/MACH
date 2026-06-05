using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace Mach.Bff.Functions;

/// <summary>
/// Reads the session tokens / anonymous id from the request cookies. The BFF never accepts tokens
/// from request bodies or query strings — only from the httpOnly cookies the Auth host sets.
/// </summary>
public sealed class CookieReader(IOptions<SessionCookieOptions> options)
{
    private readonly SessionCookieOptions _options = options.Value;

    /// <summary>Reads the customer access-token cookie (<c>mach_at</c>), or null when absent.</summary>
    public string? ReadAccessToken(HttpRequest request)
        => Read(request, _options.AccessCookieName);

    /// <summary>Reads the refresh-token cookie (<c>mach_rt</c>), or null when absent.</summary>
    public string? ReadRefreshToken(HttpRequest request)
        => Read(request, _options.RefreshCookieName);

    /// <summary>Reads the anonymous-id cookie carried for guest carts, or null when absent.</summary>
    public string? ReadAnonymousId(HttpRequest request)
        => Read(request, _options.AnonymousCookieName);

    private static string? Read(HttpRequest request, string name)
        => request.Cookies.TryGetValue(name, out var value) && !string.IsNullOrEmpty(value)
            ? value
            : null;
}
