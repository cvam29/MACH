using Microsoft.AspNetCore.Http;

namespace Mach.Bff.Functions;

/// <summary>Small helpers shared by the BFF HTTP functions.</summary>
public static class HttpRequestExtensions
{
    /// <summary>
    /// Reads and deserializes the JSON request body, returning <c>default</c> when the body is
    /// missing or malformed (callers treat that as a validation failure).
    /// </summary>
    public static async Task<T?> ReadJsonAsync<T>(this HttpRequest request, CancellationToken ct)
    {
        try
        {
            return await request.ReadFromJsonAsync<T>(ct).ConfigureAwait(false);
        }
        catch (System.Text.Json.JsonException)
        {
            return default;
        }
    }

    /// <summary>Reads a single query value, or null when absent/empty.</summary>
    public static string? Query(this HttpRequest request, string key)
        => request.Query.TryGetValue(key, out var value) && !string.IsNullOrEmpty(value)
            ? value.ToString()
            : null;

    /// <summary>Reads an integer query value with a fallback default.</summary>
    public static int QueryInt(this HttpRequest request, string key, int fallback)
        => int.TryParse(request.Query(key), out var value) ? value : fallback;
}
