using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

using Mach.Application.Dtos;
using Mach.Domain;
using Mach.Domain.ValueObjects;

using Microsoft.Extensions.Options;

namespace Mach.Infrastructure.Commercetools;

/// <summary>
/// A thin, customer-token HTTP path to the commercetools <c>/me</c> endpoints. Unlike the SDK's
/// <see cref="commercetools.Sdk.Api.Client.ProjectApiRoot"/>, which authenticates with the service
/// client-credentials token, this client sends the <em>caller's</em> customer bearer token so that
/// <c>/me</c> and <c>/me/login</c> resolve against the signed-in customer (or anonymous session)
/// rather than the service account.
/// </summary>
internal sealed class CommercetoolsCustomerApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;
    private readonly string _projectKey;

    public CommercetoolsCustomerApiClient(
        HttpClient httpClient,
        IOptions<CommercetoolsOptions> options)
    {
        _httpClient = httpClient;
        var opts = options.Value;
        _projectKey = opts.ProjectKey;

        // Base address derives from the configured API URL; per-call paths are relative to it.
        var apiUrl = opts.ApiUrl;
        if (!string.IsNullOrEmpty(apiUrl))
        {
            _httpClient.BaseAddress = new Uri(apiUrl.EndsWith('/') ? apiUrl : apiUrl + "/");
        }
    }

    /// <summary>
    /// GET <c>/{projectKey}/me</c> as the customer, mapping the profile to <see cref="CustomerDto"/>.
    /// </summary>
    public async Task<Result<CustomerDto>> GetMeAsync(string accessToken, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"{_projectKey}/me");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        try
        {
            using var response = await _httpClient.SendAsync(request, ct);
            var body = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
            {
                return Result.Failure<CustomerDto>(TranslateFailure(response.StatusCode, body));
            }

            var me = JsonSerializer.Deserialize<MeCustomer>(body, JsonOptions);
            if (me is null || string.IsNullOrEmpty(me.Id))
            {
                return Result.Failure<CustomerDto>(
                    Error.Unexpected("commercetools returned an empty /me response."));
            }

            return Result.Success(MapMe(me));
        }
        catch (HttpRequestException ex)
        {
            return Result.Failure<CustomerDto>(
                Error.Unexpected($"Could not reach the commercetools API: {ex.Message}"));
        }
    }

    /// <summary>
    /// POST <c>/{projectKey}/me/login</c> as the customer, folding the active anonymous-session cart
    /// (identified by <paramref name="anonymousId"/>) into the customer's cart.
    /// </summary>
    public async Task<Result> LoginAsync(string accessToken, string? anonymousId, CancellationToken ct)
    {
        var payload = new MeLoginRequest
        {
            AnonymousId = string.IsNullOrEmpty(anonymousId) ? null : anonymousId,
            AnonymousCartSignInMode = "MergeWithExistingCustomerCart",
            UpdateProductData = true,
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{_projectKey}/me/login")
        {
            Content = new StringContent(
                JsonSerializer.Serialize(payload, JsonOptions), Encoding.UTF8, "application/json"),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        try
        {
            using var response = await _httpClient.SendAsync(request, ct);
            var body = await response.Content.ReadAsStringAsync(ct);

            return response.IsSuccessStatusCode
                ? Result.Success()
                : Result.Failure(TranslateFailure(response.StatusCode, body));
        }
        catch (HttpRequestException ex)
        {
            return Result.Failure(
                Error.Unexpected($"Could not reach the commercetools API: {ex.Message}"));
        }
    }

    private static CustomerDto MapMe(MeCustomer me)
    {
        var addresses = (me.Addresses ?? [])
            .Select(a => new Address(
                Street: string.Join(' ', new[] { a.StreetName, a.StreetNumber }
                    .Where(s => !string.IsNullOrWhiteSpace(s))).Trim(),
                City: a.City ?? string.Empty,
                PostalCode: a.PostalCode ?? string.Empty,
                Country: a.Country ?? string.Empty,
                State: a.State ?? a.Region,
                FirstName: a.FirstName,
                LastName: a.LastName))
            .ToList();

        return new CustomerDto(
            Id: new CustomerId(me.Id),
            Email: me.Email ?? string.Empty,
            FirstName: me.FirstName ?? string.Empty,
            LastName: me.LastName ?? string.Empty,
            Addresses: addresses);
    }

    private static Error TranslateFailure(HttpStatusCode statusCode, string body)
    {
        var detail = string.IsNullOrWhiteSpace(body) ? statusCode.ToString() : body;
        return statusCode switch
        {
            HttpStatusCode.NotFound => Error.NotFound($"Resource not found: {detail}"),
            HttpStatusCode.Conflict => Error.Conflict($"The resource was modified concurrently: {detail}"),
            HttpStatusCode.BadRequest => Error.Validation($"commercetools rejected the request: {detail}"),
            HttpStatusCode.Unauthorized => Error.Validation($"Authentication with commercetools failed: {detail}"),
            HttpStatusCode.Forbidden => Error.Validation($"The operation is not permitted: {detail}"),
            >= HttpStatusCode.InternalServerError => Error.Unexpected($"commercetools returned a server error: {detail}"),
            _ => Error.Validation($"commercetools request rejected ({(int)statusCode}): {detail}"),
        };
    }

    /// <summary>Minimal JSON projection of the commercetools <c>/me</c> customer payload.</summary>
    private sealed class MeCustomer
    {
        public string Id { get; set; } = string.Empty;
        public string? Email { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public List<MeAddress>? Addresses { get; set; }
    }

    /// <summary>Minimal JSON projection of a commercetools address.</summary>
    private sealed class MeAddress
    {
        public string? StreetName { get; set; }
        public string? StreetNumber { get; set; }
        public string? City { get; set; }
        public string? PostalCode { get; set; }
        public string? Country { get; set; }
        public string? State { get; set; }
        public string? Region { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
    }

    /// <summary>The <c>/me/login</c> sign-in body (anonymous cart merge).</summary>
    private sealed class MeLoginRequest
    {
        [JsonPropertyName("anonymousId")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? AnonymousId { get; set; }

        [JsonPropertyName("anonymousCartSignInMode")]
        public string AnonymousCartSignInMode { get; set; } = "MergeWithExistingCustomerCart";

        [JsonPropertyName("updateProductData")]
        public bool UpdateProductData { get; set; }
    }
}
