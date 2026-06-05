using Mach.Domain;

using Microsoft.AspNetCore.Http;

namespace Mach.Auth.Functions;

/// <summary>
/// Maps the application's <see cref="Error"/> model onto HTTP status codes and a small
/// problem-details-shaped JSON body. The mapping is intentionally close to RFC 7807 without taking a
/// dependency on the framework's <c>ProblemDetails</c> type.
/// </summary>
public static class ResultHttp
{
    /// <summary>The error code commercetools auth failures surface for bad credentials/tokens.</summary>
    public const string AuthFailureCode = "auth_failed";

    /// <summary>Resolves the HTTP status code for an <see cref="Error"/>.</summary>
    public static int StatusCodeFor(Error error) => error.Code switch
    {
        "validation" => StatusCodes.Status400BadRequest,
        AuthFailureCode => StatusCodes.Status401Unauthorized,
        "not_found" => StatusCodes.Status404NotFound,
        "conflict" => StatusCodes.Status409Conflict,
        "unexpected" => StatusCodes.Status500InternalServerError,
        _ => StatusCodes.Status500InternalServerError,
    };

    /// <summary>Builds an <see cref="IResult"/> problem response for <paramref name="error"/>.</summary>
    public static IResult Problem(Error error)
    {
        var status = StatusCodeFor(error);
        return Results.Json(
            new ProblemBody(TitleFor(status), status, error.Code, error.Message),
            statusCode: status);
    }

    /// <summary>Builds an explicit 401 problem (e.g. missing/expired session cookies).</summary>
    public static IResult Unauthorized(string message)
        => Results.Json(
            new ProblemBody("Unauthorized", StatusCodes.Status401Unauthorized, AuthFailureCode, message),
            statusCode: StatusCodes.Status401Unauthorized);

    private static string TitleFor(int status) => status switch
    {
        StatusCodes.Status400BadRequest => "Bad Request",
        StatusCodes.Status401Unauthorized => "Unauthorized",
        StatusCodes.Status404NotFound => "Not Found",
        StatusCodes.Status409Conflict => "Conflict",
        _ => "Internal Server Error",
    };

    /// <summary>Problem-details-ish body. <c>code</c> carries the stable application error token.</summary>
    public sealed record ProblemBody(string Title, int Status, string Code, string Detail);
}
