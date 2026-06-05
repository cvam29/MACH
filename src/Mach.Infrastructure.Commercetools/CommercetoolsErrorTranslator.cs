using commercetools.Base.Client.Error;

using Mach.Domain;

namespace Mach.Infrastructure.Commercetools;

/// <summary>
/// Translates commercetools client exceptions into the application's <see cref="Error"/> model so
/// callers receive <see cref="Result"/> failures instead of raw transport exceptions.
/// </summary>
internal static class CommercetoolsErrorTranslator
{
    public static Error Translate(Exception exception) => exception switch
    {
        NotFoundException nf => Error.NotFound(Describe(nf, "Resource not found.")),

        // Optimistic-concurrency: the supplied version did not match the current version.
        ConcurrentModificationException cm => Error.Conflict(Describe(cm, "The resource was modified concurrently; retry with the current version.")),

        BadRequestException br => Error.Validation(Describe(br, "The request was rejected by commercetools.")),

        UnauthorizedException ua => Error.Validation(Describe(ua, "Authentication with commercetools failed.")),

        ForbiddenException fb => Error.Validation(Describe(fb, "The operation is not permitted.")),

        ApiServerException se => Error.Unexpected(Describe(se, "commercetools returned a server error.")),

        ApiClientException ce => Error.Validation(Describe(ce, "commercetools rejected the request.")),

        _ => Error.Unexpected(exception.Message),
    };

    private static string Describe(Exception exception, string fallback)
    {
        var message = exception.Message;
        return string.IsNullOrWhiteSpace(message) ? fallback : message;
    }
}
