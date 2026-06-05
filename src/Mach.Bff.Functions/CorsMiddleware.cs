using Microsoft.AspNetCore.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Middleware;
using Microsoft.Extensions.Options;

namespace Mach.Bff.Functions;

/// <summary>
/// Adds credentialed CORS headers for the configured storefront origin(s) and short-circuits
/// preflight (<c>OPTIONS</c>) requests. Applied as Functions worker middleware so it runs for the
/// ASP.NET Core integration's HTTP pipeline. Because credentials are allowed, the
/// <c>Access-Control-Allow-Origin</c> header echoes the request origin (never <c>*</c>).
/// </summary>
public sealed class CorsMiddleware(IOptions<CorsOptions> options) : IFunctionsWorkerMiddleware
{
    private readonly HashSet<string> _allowedOrigins =
        new(options.Value.AllowedOrigins, StringComparer.OrdinalIgnoreCase);

    public async Task Invoke(FunctionContext context, FunctionExecutionDelegate next)
    {
        var httpContext = context.GetHttpContext();
        if (httpContext is null)
        {
            await next(context);
            return;
        }

        var request = httpContext.Request;
        var response = httpContext.Response;
        var origin = request.Headers.Origin.ToString();

        var originAllowed = !string.IsNullOrEmpty(origin) && _allowedOrigins.Contains(origin);
        if (originAllowed)
        {
            var headers = response.Headers;
            headers.AccessControlAllowOrigin = origin;
            headers.AccessControlAllowCredentials = "true";
            headers.Vary = "Origin";

            if (HttpMethods.IsOptions(request.Method))
            {
                headers.AccessControlAllowMethods = "GET, POST, PATCH, PUT, DELETE, OPTIONS";
                headers.AccessControlAllowHeaders =
                    request.Headers.AccessControlRequestHeaders.Count > 0
                        ? request.Headers.AccessControlRequestHeaders.ToString()
                        : "Content-Type, Idempotency-Key";
                headers.AccessControlMaxAge = "600";
                response.StatusCode = StatusCodes.Status204NoContent;
                return;
            }
        }

        await next(context);
    }
}
