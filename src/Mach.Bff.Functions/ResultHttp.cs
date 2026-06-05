using Mach.Domain;

using Microsoft.AspNetCore.Http;

namespace Mach.Bff.Functions;

/// <summary>
/// Maps the application's <see cref="Error"/> model onto HTTP status codes and a small
/// problem-details-shaped JSON body. Intentionally close to RFC 7807 without depending on the
/// framework's <c>ProblemDetails</c> type.
/// </summary>
public static class ResultHttp
{
    /// <summary>Resolves the HTTP status code for an <see cref="Error"/>.</summary>
    public static int StatusCodeFor(Error error) => error.Code switch
    {
        "validation" => StatusCodes.Status400BadRequest,
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
            new ProblemBody("Unauthorized", StatusCodes.Status401Unauthorized, "unauthorized", message),
            statusCode: StatusCodes.Status401Unauthorized);

    /// <summary>Builds a 400 problem for a malformed/invalid request that never reached a port.</summary>
    public static IResult BadRequest(string message)
        => Problem(Error.Validation(message));

    /// <summary>Maps a <see cref="Result{T}"/> onto a 200 with its value, or a problem response.</summary>
    public static IResult Ok<T>(Result<T> result)
        => result.IsSuccess ? Results.Ok(result.Value) : Problem(result.Error);

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
