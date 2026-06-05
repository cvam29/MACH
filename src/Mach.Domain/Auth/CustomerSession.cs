using Mach.Domain.ValueObjects;

namespace Mach.Domain.Auth;

/// <summary>
/// A customer (or anonymous) session backed by commercetools OAuth2 tokens.
/// Tokens are handed to the storefront only via httpOnly cookies.
/// </summary>
public sealed record CustomerSession(
    string AccessToken,
    string RefreshToken,
    DateTimeOffset ExpiresAt,
    CustomerId? CustomerId,
    string? AnonymousId)
{
    /// <summary>True when the session represents a guest (no customer id, has an anonymous id).</summary>
    public bool IsAnonymous => CustomerId is null && !string.IsNullOrEmpty(AnonymousId);
}

/// <summary>Login credentials for the commercetools password flow.</summary>
public sealed record Credentials(string Email, string Password);

/// <summary>A new-customer registration request.</summary>
public sealed record RegisterRequest(
    string Email,
    string Password,
    string FirstName,
    string LastName,
    string? AnonymousId = null);
