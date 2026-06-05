using Mach.Application.Ports;
using Mach.Domain;
using Mach.Domain.Auth;

using Microsoft.AspNetCore.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace Mach.Auth.Functions;

/// <summary>
/// The customer-auth HTTP API. Tokens are exchanged with commercetools via <see cref="ICustomerAuth"/>
/// and handed to the storefront ONLY through the httpOnly session cookies managed by
/// <see cref="AuthCookieWriter"/>. The effective route prefix is <c>/api</c>, so the functions below
/// are reachable at <c>/api/auth/*</c>.
/// </summary>
public sealed class AuthFunctions(
    ICustomerAuth customerAuth,
    AuthCookieWriter cookies,
    ILogger<AuthFunctions> logger)
{
    [Function("Register")]
    public async Task<IResult> Register(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "auth/register")] HttpRequest request,
        CancellationToken ct)
    {
        var body = await ReadJsonAsync<RegisterBody>(request, ct);
        if (body is null
            || string.IsNullOrWhiteSpace(body.Email)
            || string.IsNullOrWhiteSpace(body.Password)
            || string.IsNullOrWhiteSpace(body.FirstName)
            || string.IsNullOrWhiteSpace(body.LastName))
        {
            return ResultHttp.Problem(Error.Validation(
                "Email, password, firstName and lastName are required."));
        }

        var registerRequest = new RegisterRequest(
            body.Email, body.Password, body.FirstName, body.LastName, body.AnonymousId);

        var result = await customerAuth.RegisterAsync(registerRequest, ct);
        if (result.IsFailure)
        {
            return ResultHttp.Problem(result.Error);
        }

        cookies.SetSessionCookies(request.HttpContext.Response, result.Value);
        return Results.Json(SessionSummary.From(result.Value), statusCode: StatusCodes.Status201Created);
    }

    [Function("Login")]
    public async Task<IResult> Login(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "auth/login")] HttpRequest request,
        CancellationToken ct)
    {
        var body = await ReadJsonAsync<LoginBody>(request, ct);
        if (body is null || string.IsNullOrWhiteSpace(body.Email) || string.IsNullOrWhiteSpace(body.Password))
        {
            return ResultHttp.Problem(Error.Validation("Email and password are required."));
        }

        var result = await customerAuth.LoginAsync(new Credentials(body.Email, body.Password), ct);
        if (result.IsFailure)
        {
            return ResultHttp.Problem(result.Error);
        }

        cookies.SetSessionCookies(request.HttpContext.Response, result.Value);
        return Results.Ok(SessionSummary.From(result.Value));
    }

    [Function("Refresh")]
    public async Task<IResult> Refresh(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "auth/refresh")] HttpRequest request,
        CancellationToken ct)
    {
        var refreshToken = cookies.ReadRefreshToken(request);
        if (refreshToken is null)
        {
            return ResultHttp.Unauthorized("No refresh session cookie present.");
        }

        var result = await customerAuth.RefreshAsync(refreshToken, ct);
        if (result.IsFailure)
        {
            // A failed refresh means the session is no longer valid: clear cookies and report 401.
            cookies.ClearSessionCookies(request.HttpContext.Response);
            return ResultHttp.Unauthorized("The session could not be refreshed.");
        }

        cookies.SetSessionCookies(request.HttpContext.Response, result.Value);
        return Results.Ok(SessionSummary.From(result.Value));
    }

    [Function("AnonymousSession")]
    public async Task<IResult> AnonymousSession(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "auth/anonymous")] HttpRequest request,
        CancellationToken ct)
    {
        var result = await customerAuth.AnonymousSessionAsync(ct);
        if (result.IsFailure)
        {
            return ResultHttp.Problem(result.Error);
        }

        cookies.SetSessionCookies(request.HttpContext.Response, result.Value);
        return Results.Ok(SessionSummary.From(result.Value));
    }

    [Function("Logout")]
    public IResult Logout(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "auth/logout")] HttpRequest request)
    {
        // Best-effort: clearing the cookies signs the browser out immediately. commercetools tokens
        // are short-lived and self-expire, so there is no server-side revoke to perform here.
        cookies.ClearSessionCookies(request.HttpContext.Response);
        return Results.NoContent();
    }

    [Function("Me")]
    public async Task<IResult> Me(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "auth/me")] HttpRequest request,
        CancellationToken ct)
    {
        var accessToken = cookies.ReadAccessToken(request);
        var refreshToken = cookies.ReadRefreshToken(request);

        if (accessToken is not null)
        {
            var meResult = await customerAuth.GetMeAsync(accessToken, ct);
            if (meResult.IsSuccess)
            {
                return Results.Ok(ProfileResponse.From(meResult.Value));
            }

            // Access token likely expired; fall through to a transparent refresh if we can.
            logger.LogDebug("GetMe with access cookie failed ({Code}); attempting refresh.", meResult.Error.Code);
        }

        if (refreshToken is null)
        {
            return ResultHttp.Unauthorized("No valid session.");
        }

        var refreshed = await customerAuth.RefreshAsync(refreshToken, ct);
        if (refreshed.IsFailure)
        {
            cookies.ClearSessionCookies(request.HttpContext.Response);
            return ResultHttp.Unauthorized("The session has expired.");
        }

        // Reset cookies to the freshly minted tokens, then retry /me with the new access token.
        cookies.SetSessionCookies(request.HttpContext.Response, refreshed.Value);

        var retry = await customerAuth.GetMeAsync(refreshed.Value.AccessToken, ct);
        if (retry.IsFailure)
        {
            return ResultHttp.Problem(retry.Error);
        }

        return Results.Ok(ProfileResponse.From(retry.Value));
    }

    private static async Task<T?> ReadJsonAsync<T>(HttpRequest request, CancellationToken ct)
    {
        try
        {
            return await request.ReadFromJsonAsync<T>(ct);
        }
        catch (System.Text.Json.JsonException)
        {
            return default;
        }
    }
}
