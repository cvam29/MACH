using commercetools.Base.Client;
using commercetools.Base.Client.Domain;
using commercetools.Base.Client.Tokens;

namespace Mach.Infrastructure.Commercetools;

/// <summary>
/// An <see cref="ITokenProvider"/> that wraps a pre-obtained customer (or anonymous-session) access
/// token. The OAuth2 token has already been minted by <see cref="CommercetoolsTokenClient"/>; this
/// provider simply hands that bearer token to the SDK's authorization middleware so the SDK's own
/// HTTP + serialization pipeline issues <c>/me</c> calls on the caller's behalf. No token endpoint
/// is contacted — the SDK never refreshes a token it did not mint.
/// </summary>
internal sealed class CustomerAccessTokenProvider : ITokenProvider
{
    private readonly Token _token;

    public CustomerAccessTokenProvider(string accessToken)
    {
        // The SDK's AuthorizationMiddleware only reads Token.AccessToken to build the
        // "Authorization: Bearer {AccessToken}" header; the remaining fields are informational.
        _token = new Token
        {
            AccessToken = accessToken,
            TokenType = "Bearer",
        };
    }

    /// <summary>The customer flow most closely matches a pre-obtained customer token.</summary>
    public TokenFlow TokenFlow => TokenFlow.Password;

    public Token Token => _token;

    public IClientConfiguration ClientConfiguration { get; set; } = null!;

    public Task<Token> GetToken() => Task.FromResult(_token);
}
