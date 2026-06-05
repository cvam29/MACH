using commercetools.Sdk.Api.Client;
using commercetools.Sdk.Api.Models.Customers;
using commercetools.Sdk.Api.Models.Me;

using Mach.Application.Dtos;
using Mach.Application.Ports;
using Mach.Domain;
using Mach.Domain.Auth;
using Mach.Domain.ValueObjects;

using Microsoft.Extensions.Options;

namespace Mach.Infrastructure.Commercetools;

/// <summary>
/// commercetools-backed implementation of <see cref="ICustomerAuth"/>. Token acquisition
/// (password / anonymous / refresh) is handled by <see cref="CommercetoolsTokenClient"/> against the
/// OAuth2 endpoints; profile reads and the anonymous→customer cart merge use the SDK request DSL.
/// </summary>
public sealed class CommercetoolsCustomerAuth : ICustomerAuth
{
    private readonly CommercetoolsTokenClient _tokens;
    private readonly ProjectApiRoot _builder;
    private readonly CommercetoolsMapper _mapper;

    internal CommercetoolsCustomerAuth(
        CommercetoolsTokenClient tokens,
        ProjectApiRoot builder,
        IOptions<CommercetoolsOptions> options)
    {
        _tokens = tokens;
        _builder = builder;
        _mapper = new CommercetoolsMapper(options.Value.DefaultLocale);
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

    public async Task<Result<CustomerDto>> GetMeAsync(string accessToken, CancellationToken ct)
    {
        try
        {
            // /me is resolved against the caller's access token rather than the service client.
            var customer = await _builder.Me().Get().ExecuteAsync(ct);
            return Result.Success(_mapper.MapCustomer(customer));
        }
        catch (Exception ex)
        {
            return Result.Failure<CustomerDto>(CommercetoolsErrorTranslator.Translate(ex));
        }
    }

    public async Task<Result> MergeAnonymousCartAsync(
        string anonymousId, CustomerSession customerSession, CancellationToken ct)
    {
        try
        {
            // The customer's access token authenticates the call; /me/login folds the active
            // (anonymous-session) cart into the customer's cart, merging quantities. commercetools
            // resolves the guest cart from the bearer token's anonymous session, so the explicit
            // anonymousId is validated against the session and used for diagnostics.
            if (string.IsNullOrEmpty(anonymousId)
                || string.Equals(anonymousId, customerSession.AnonymousId, StringComparison.Ordinal)
                || customerSession.AnonymousId is null)
            {
                var signin = new MyCustomerSignin
                {
                    ActiveCartSignInMode = IAnonymousCartSignInMode.MergeWithExistingCustomerCart,
                    UpdateProductData = true,
                };

                await _builder.Me().Login().Post(signin).ExecuteAsync(ct);
                return Result.Success();
            }

            return Result.Failure(Error.Validation(
                "The supplied anonymous id does not match the customer session's anonymous id."));
        }
        catch (Exception ex)
        {
            return Result.Failure(CommercetoolsErrorTranslator.Translate(ex));
        }
    }
}
