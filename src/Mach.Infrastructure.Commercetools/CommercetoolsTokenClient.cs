using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

using Mach.Domain;
using Mach.Domain.Auth;
using Mach.Domain.ValueObjects;

using Microsoft.Extensions.Options;

namespace Mach.Infrastructure.Commercetools;

/// <summary>
/// Drives the commercetools OAuth2 token endpoints directly: the customer password flow, the
/// anonymous-session flow and the refresh-token flow. Kept separate from the SDK so the flows are
/// straightforward to exercise against a mock auth server.
/// </summary>
internal sealed class CommercetoolsTokenClient(
    HttpClient httpClient,
    IOptions<CommercetoolsOptions> options)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly CommercetoolsOptions _options = options.Value;

    /// <summary>OAuth2 customer password flow → customer access + refresh tokens.</summary>
    public Task<Result<CustomerSession>> PasswordAsync(
        Credentials credentials, CancellationToken ct)
    {
        var form = new Dictionary<string, string>
        {
            ["grant_type"] = "password",
            ["username"] = credentials.Email,
            ["password"] = credentials.Password,
        };
        if (!string.IsNullOrEmpty(_options.ScopeList))
        {
            form["scope"] = _options.ScopeList;
        }

        return RequestSessionAsync(_options.CustomerTokenEndpoint, form, anonymous: false, ct);
    }

    /// <summary>OAuth2 anonymous-session flow → tokens scoped to a freshly minted anonymous id.</summary>
    public Task<Result<CustomerSession>> AnonymousAsync(CancellationToken ct)
    {
        var form = new Dictionary<string, string> { ["grant_type"] = "client_credentials" };
        if (!string.IsNullOrEmpty(_options.ScopeList))
        {
            form["scope"] = _options.ScopeList;
        }

        return RequestSessionAsync(_options.AnonymousTokenEndpoint, form, anonymous: true, ct);
    }

    /// <summary>OAuth2 refresh-token flow → a new access token for an existing session.</summary>
    public Task<Result<CustomerSession>> RefreshAsync(string refreshToken, CancellationToken ct)
    {
        var form = new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = refreshToken,
        };

        // The refresh response does not re-issue a refresh token; carry the supplied one forward.
        return RequestSessionAsync(_options.TokenEndpoint, form, anonymous: false, ct, carriedRefreshToken: refreshToken);
    }

    private async Task<Result<CustomerSession>> RequestSessionAsync(
        string endpoint,
        Dictionary<string, string> form,
        bool anonymous,
        CancellationToken ct,
        string? carriedRefreshToken = null)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
            {
                Content = new FormUrlEncodedContent(form),
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", BasicCredentials());

            using var response = await httpClient.SendAsync(request, ct);
            var body = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
            {
                return Result.Failure<CustomerSession>(TranslateAuthFailure(response.StatusCode, body));
            }

            var token = JsonSerializer.Deserialize<OAuthTokenResponse>(body, JsonOptions);
            if (token is null || string.IsNullOrEmpty(token.AccessToken))
            {
                return Result.Failure<CustomerSession>(
                    Error.Unexpected("The auth server returned an empty token response."));
            }

            var session = new CustomerSession(
                AccessToken: token.AccessToken,
                RefreshToken: token.RefreshToken ?? carriedRefreshToken ?? string.Empty,
                ExpiresAt: DateTimeOffset.UtcNow.AddSeconds(token.ExpiresIn),
                CustomerId: null,
                AnonymousId: anonymous ? token.AnonymousId : null);

            return Result.Success(session);
        }
        catch (HttpRequestException ex)
        {
            return Result.Failure<CustomerSession>(
                Error.Unexpected($"Could not reach the commercetools auth server: {ex.Message}"));
        }
    }

    private static Error TranslateAuthFailure(System.Net.HttpStatusCode statusCode, string body)
    {
        var detail = string.IsNullOrWhiteSpace(body) ? statusCode.ToString() : body;
        return statusCode switch
        {
            System.Net.HttpStatusCode.BadRequest => Error.Validation($"Invalid credentials or grant: {detail}"),
            System.Net.HttpStatusCode.Unauthorized => Error.Validation($"Authentication failed: {detail}"),
            >= System.Net.HttpStatusCode.InternalServerError => Error.Unexpected($"Auth server error: {detail}"),
            _ => Error.Validation($"Token request rejected ({(int)statusCode}): {detail}"),
        };
    }

    private string BasicCredentials() => Convert.ToBase64String(
        Encoding.UTF8.GetBytes($"{_options.ClientId}:{_options.ClientSecret}"));
}
