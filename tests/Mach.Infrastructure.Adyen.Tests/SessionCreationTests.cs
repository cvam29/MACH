using System.Text.Json;

using Mach.Domain.ValueObjects;
using Mach.Infrastructure.Adyen;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Shouldly;

using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

using HmacValidator = global::Adyen.Util.HmacValidator;

namespace Mach.Infrastructure.Adyen.Tests;

/// <summary>
/// Session-creation tests that point the real <see cref="HttpAdyenCheckoutApi"/> at a WireMock
/// server returning a canned <c>/sessions</c> response. Asserts the outbound request is
/// well-formed (Adyen JSON shape, API key header) and the response maps to our DTO.
/// </summary>
public sealed class SessionCreationTests : IDisposable
{
    private readonly WireMockServer _server = WireMockServer.Start();

    [Fact]
    public async Task CreatePaymentSession_posts_wellformed_request_and_maps_response()
    {
        const string cannedResponse = """
        {
          "amount": { "currency": "EUR", "value": 4999 },
          "expiresAt": "2026-06-05T12:00:00Z",
          "id": "CS-SESSION-123",
          "merchantAccount": "TestMerchant",
          "reference": "cart-777",
          "returnUrl": "https://shop.example/return",
          "sessionData": "Ab02b4c0!BQABAgABCDEF...",
          "mode": "embedded"
        }
        """;

        _server
            .Given(Request.Create().WithPath("/v71/sessions").UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody(cannedResponse));

        var gateway = BuildGateway(checkoutEndpointOverride: $"{_server.Url}/v71");

        var result = await gateway.CreatePaymentSessionAsync(
            new CartId("cart-777"), new Money(49.99m, "EUR"), CancellationToken.None);

        // Response mapped to our DTO.
        result.IsSuccess.ShouldBeTrue(result.IsFailure ? result.Error.Message : null);
        result.Value.SessionId.ShouldBe("CS-SESSION-123");
        result.Value.SessionData.ShouldBe("Ab02b4c0!BQABAgABCDEF...");
        result.Value.CartId.Value.ShouldBe("cart-777");
        result.Value.Amount.Currency.ShouldBe("EUR");
        result.Value.Amount.Amount.ShouldBe(49.99m);

        // Outbound request was well-formed.
        var log = _server.LogEntries.Single();
        var request = log.RequestMessage.ShouldNotBeNull();
        var headers = request.Headers.ShouldNotBeNull();
        headers["x-API-key"].ShouldContain("test-api-key");

        using var sent = JsonDocument.Parse(request.Body.ShouldNotBeNull());
        var root = sent.RootElement;
        root.GetProperty("merchantAccount").GetString().ShouldBe("TestMerchant");
        root.GetProperty("reference").GetString().ShouldBe("cart-777");
        root.GetProperty("returnUrl").GetString().ShouldBe("https://shop.example/return");
        root.GetProperty("amount").GetProperty("currency").GetString().ShouldBe("EUR");
        root.GetProperty("amount").GetProperty("value").GetInt64().ShouldBe(4999);
    }

    [Fact]
    public async Task CreatePaymentSession_returns_failure_on_http_error()
    {
        _server
            .Given(Request.Create().WithPath("/v71/sessions").UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(401)
                .WithBody("""{"status":401,"errorCode":"171","message":"Unauthorized"}"""));

        var gateway = BuildGateway(checkoutEndpointOverride: $"{_server.Url}/v71");

        var result = await gateway.CreatePaymentSessionAsync(
            new CartId("cart-1"), new Money(10m, "EUR"), CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("unexpected");
    }

    private static AdyenPaymentGateway BuildGateway(string checkoutEndpointOverride)
    {
        var options = Options.Create(new AdyenOptions
        {
            ApiKey = "test-api-key",
            MerchantAccount = "TestMerchant",
            HmacKey = "00",
            Environment = "Test",
            ReturnUrl = "https://shop.example/return",
            CheckoutEndpointOverride = checkoutEndpointOverride,
        });

        var api = new HttpAdyenCheckoutApi(new HttpClient(), options);

        return new AdyenPaymentGateway(
            api, new HmacValidator(), options, NullLogger<AdyenPaymentGateway>.Instance);
    }

    public void Dispose() => _server.Dispose();
}
