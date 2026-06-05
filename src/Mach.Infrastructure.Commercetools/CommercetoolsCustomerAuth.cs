using commercetools.Sdk.Api.Client;
using commercetools.Sdk.Api.Models.Me;

using Mach.Application.Dtos;
using Mach.Application.Ports;
using Mach.Domain;
using Mach.Domain.Auth;
using Mach.Domain.ValueObjects;

namespace Mach.Infrastructure.Commercetools;

/// <summary>
/// commercetools-backed implementation of <see cref="ICustomerAuth"/>. Token acquisition
/// (password / anonymous / refresh) is handled by <see cref="CommercetoolsTokenClient"/> against the
/// OAuth2 endpoints; registration uses the service-credentialed SDK request DSL, while the
/// customer-scoped <c>/me</c> reads and the anonymous→customer cart merge go through the
/// <see cref="CommercetoolsCustomerApiClient"/> so they carry the <em>caller's</em> bearer token.
/// </summary>
public sealed class CommercetoolsCustomerAuth : ICustomerAuth
{
    private readonly CommercetoolsTokenClient _tokens;
    private readonly ProjectApiRoot _builder;
    private readonly CommercetoolsCustomerApiClient _customerApi;

    internal CommercetoolsCustomerAuth(
        CommercetoolsTokenClient tokens,
        ProjectApiRoot builder,
        CommercetoolsCustomerApiClient customerApi)
    {
        _tokens = tokens;
        _builder = builder;
        _customerApi = customerApi;
    }

    public async Task<Result<CustomerSession>> RegisterAsync(RegisterRequest request, CancellationToken ct)
    {
        try
        {
            var draft = new MyCustomerDraft
            {
                Email = request.Email,
                Password = request.Password,
                FirstName = request.FirstName,
                LastName = request.LastName,
            };

            var signupResult = await _builder.Me().Signup().Post(draft).ExecuteAsync(ct);

            // Signup creates the customer but does not mint customer tokens; perform the password
            // flow so the caller receives a usable session with the new customer's id attached.
            var loginResult = await _tokens.PasswordAsync(
                new Credentials(request.Email, request.Password), ct);
            if (loginResult.IsFailure)
            {
                return loginResult;
            }

            var session = loginResult.Value with
            {
                CustomerId = new CustomerId(signupResult.Customer.Id),
            };
            return Result.Success(session);
        }
        catch (Exception ex)
        {
            return Result.Failure<CustomerSession>(CommercetoolsErrorTranslator.Translate(ex));
        }
    }

    public async Task<Result<CustomerSession>> LoginAsync(Credentials credentials, CancellationToken ct)
    {
        var tokenResult = await _tokens.PasswordAsync(credentials, ct);
        if (tokenResult.IsFailure)
        {
            return tokenResult;
        }

        // Resolve the customer id via /me so the session carries it.
        var meResult = await GetMeAsync(tokenResult.Value.AccessToken, ct);
        if (meResult.IsFailure)
        {
            return Result.Success(tokenResult.Value);
        }

        var session = tokenResult.Value with { CustomerId = meResult.Value.Id };
        return Result.Success(session);
    }

    public Task<Result<CustomerSession>> RefreshAsync(string refreshToken, CancellationToken ct)
        => _tokens.RefreshAsync(refreshToken, ct);

    public Task<Result<CustomerSession>> AnonymousSessionAsync(CancellationToken ct)
        => _tokens.AnonymousAsync(ct);

    public Task<Result<CustomerDto>> GetMeAsync(string accessToken, CancellationToken ct)
        // /me is resolved against the caller's customer bearer token, not the service client.
        => _customerApi.GetMeAsync(accessToken, ct);

    public Task<Result> MergeAnonymousCartAsync(
        string anonymousId, CustomerSession customerSession, CancellationToken ct)
    {
        // The customer's access token authenticates the call; /me/login folds the active
        // (anonymous-session) cart into the customer's cart, merging quantities. commercetools
        // resolves the guest cart from the bearer token's anonymous session, so the explicit
        // anonymousId is validated against the session before the call is made.
        if (!string.IsNullOrEmpty(anonymousId)
            && customerSession.AnonymousId is not null
            && !string.Equals(anonymousId, customerSession.AnonymousId, StringComparison.Ordinal))
        {
            return Task.FromResult(Result.Failure(Error.Validation(
                "The supplied anonymous id does not match the customer session's anonymous id.")));
        }

        return _customerApi.LoginAsync(customerSession.AccessToken, anonymousId, ct);
    }
}
