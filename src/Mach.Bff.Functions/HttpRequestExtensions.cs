using Microsoft.AspNetCore.Http;

namespace Mach.Bff.Functions;

/// <summary>Small helpers shared by the BFF HTTP functions (C# 14 extension members).</summary>
public static class HttpRequestExtensions
{
    extension(HttpRequest request)
    {
        /// <summary>
        /// Reads and deserializes the JSON request body, returning <c>default</c> when the body is
        /// missing or malformed (callers treat that as a validation failure).
        /// </summary>
        public async Task<T?> ReadJsonAsync<T>(CancellationToken ct)
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
        public string? QueryValue(string key)
            => request.Query.TryGetValue(key, out var value) && !string.IsNullOrEmpty(value)
                ? value.ToString()
                : null;

        /// <summary>Reads an integer query value with a fallback default.</summary>
        public int QueryInt(string key, int fallback)
            => int.TryParse(request.QueryValue(key), out var value) ? value : fallback;
    }
}
