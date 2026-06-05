using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

using Microsoft.Extensions.Options;

using CreateCheckoutSessionRequest = Adyen.Checkout.Models.CreateCheckoutSessionRequest;
using CreateCheckoutSessionResponse = Adyen.Checkout.Models.CreateCheckoutSessionResponse;

namespace Mach.Infrastructure.Adyen;

/// <summary>
/// Default <see cref="IAdyenCheckoutApi"/> implementation that posts to the Adyen Checkout
/// <c>/sessions</c> endpoint over a configured <see cref="HttpClient"/>, serializing requests and
/// responses with the official Adyen Checkout models. The base URL is resolved from
/// <see cref="AdyenOptions"/> so it can be redirected to a WireMock server in tests.
/// </summary>
internal sealed class HttpAdyenCheckoutApi : IAdyenCheckoutApi
{
    private readonly HttpClient _httpClient;
    private readonly AdyenOptions _options;

    public HttpAdyenCheckoutApi(HttpClient httpClient, IOptions<AdyenOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
    }

    public async Task<CreateCheckoutSessionResponse> CreateSessionAsync(
        CreateCheckoutSessionRequest request, CancellationToken ct)
    {
        var baseUrl = AdyenEndpoints.ResolveCheckoutBaseUrl(_options);
        var uri = $"{baseUrl}/{AdyenEndpoints.SessionsPath}";

        using var message = new HttpRequestMessage(HttpMethod.Post, uri)
        {
            Content = JsonContent.Create(request, options: AdyenJson.Checkout),
        };
        message.Headers.Add("x-API-key", _options.ApiKey);
        message.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        using var response = await _httpClient.SendAsync(message, ct).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"Adyen /sessions returned {(int)response.StatusCode}: {body}");
        }

        var session = JsonSerializer.Deserialize<CreateCheckoutSessionResponse>(body, AdyenJson.Checkout);
        return session
            ?? throw new InvalidOperationException("Adyen returned an empty /sessions response body.");
    }
}
