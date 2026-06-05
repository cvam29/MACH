using Microsoft.AspNetCore.Http;

namespace Mach.Webhooks.Functions;

/// <summary>Small helpers for reading raw inbound webhook payloads.</summary>
internal static class WebhookRequest
{
    /// <summary>Reads the full request body as a UTF-8 string without consuming framework buffering.</summary>
    public static async Task<string> ReadRawBodyAsync(HttpRequest request, CancellationToken ct)
    {
        request.EnableBuffering();
        using var reader = new StreamReader(
            request.Body,
            encoding: System.Text.Encoding.UTF8,
            detectEncodingFromByteOrderMarks: false,
            bufferSize: 1024,
            leaveOpen: true);

        var body = await reader.ReadToEndAsync(ct).ConfigureAwait(false);
        request.Body.Position = 0;
        return body;
    }
}
