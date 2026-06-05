using Mach.Application.Dtos;
using Mach.Domain;
using Mach.Domain.Auth;

namespace Mach.Application.Ports;

/// <summary>
/// Port over commercetools customer authentication (OAuth2 password flow, anonymous sessions,
/// <c>/me</c> endpoints, anonymous→customer cart merge). Implemented by
/// <c>Mach.Infrastructure.Commercetools</c>.
/// </summary>
public interface ICustomerAuth
{
    Task<Result<CustomerSession>> RegisterAsync(RegisterRequest request, CancellationToken ct);

    /// <summary>commercetools OAuth2 password flow → customer access + refresh tokens.</summary>
    Task<Result<CustomerSession>> LoginAsync(Credentials credentials, CancellationToken ct);

    Task<Result<CustomerSession>> RefreshAsync(string refreshToken, CancellationToken ct);

    /// <summary>Mint an anonymous session so a guest gets an anonymous-id-scoped cart.</summary>
    Task<Result<CustomerSession>> AnonymousSessionAsync(CancellationToken ct);

    /// <summary>Resolve the caller's profile via the commercetools <c>/me</c> endpoint.</summary>
    Task<Result<CustomerDto>> GetMeAsync(string accessToken, CancellationToken ct);

    /// <summary>Merge a guest's anonymous cart into the signed-in customer's cart.</summary>
    Task<Result> MergeAnonymousCartAsync(
        string anonymousId, CustomerSession customerSession, CancellationToken ct);
}
