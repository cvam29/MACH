namespace Mach.Bff.Functions;

/// <summary>
/// Names of the session cookies the BFF reads (never writes — the Auth host owns minting them).
/// Bound from the <c>Auth:</c> configuration section so both hosts share the same cookie names.
/// </summary>
public sealed class SessionCookieOptions
{
    /// <summary>Configuration section name (shared with the Auth host).</summary>
    public const string SectionName = "Auth";

    /// <summary>Name of the access-token cookie minted by the Auth host.</summary>
    public string AccessCookieName { get; set; } = "mach_at";

    /// <summary>Name of the refresh-token cookie minted by the Auth host.</summary>
    public string RefreshCookieName { get; set; } = "mach_rt";

    /// <summary>Name of the anonymous-id cookie carried for guest carts.</summary>
    public string AnonymousCookieName { get; set; } = "mach_anon";
}
