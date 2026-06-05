using Mach.Application.Dtos;
using Mach.Domain.Auth;

namespace Mach.Auth.Functions;

/// <summary>Request body for <c>POST /auth/register</c>.</summary>
public sealed record RegisterBody(
    string? Email,
    string? Password,
    string? FirstName,
    string? LastName,
    string? AnonymousId);

/// <summary>Request body for <c>POST /auth/login</c>.</summary>
public sealed record LoginBody(string? Email, string? Password);

/// <summary>
/// A non-sensitive view of the established session. Tokens are NEVER included here — they are
/// delivered solely via httpOnly cookies.
/// </summary>
public sealed record SessionSummary(
    bool Authenticated,
    bool Anonymous,
    string? CustomerId,
    string? AnonymousId,
    DateTimeOffset ExpiresAt)
{
    public static SessionSummary From(CustomerSession session) => new(
        Authenticated: session.CustomerId is not null,
        Anonymous: session.IsAnonymous,
        CustomerId: session.CustomerId?.Value,
        AnonymousId: session.AnonymousId,
        ExpiresAt: session.ExpiresAt);
}

/// <summary>The signed-in customer's profile returned by <c>GET /auth/me</c>.</summary>
public sealed record ProfileResponse(
    string Id,
    string Email,
    string FirstName,
    string LastName)
{
    public static ProfileResponse From(CustomerDto customer) => new(
        Id: customer.Id.Value,
        Email: customer.Email,
        FirstName: customer.FirstName,
        LastName: customer.LastName);
}
