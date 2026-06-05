using System.Text.Json;

using Mach.Application.Ports;
using Mach.Domain.ValueObjects;
using Mach.Infrastructure.Adyen;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using Shouldly;

using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace Mach.Infrastructure.Adyen.Tests;

/// <summary>
/// Session-creation tests that drive the real Adyen .NET SDK checkout service (its Sessions API)
/// configured — through the SDK's own <c>HttpClient</c> surface — to hit a WireMock <c>/sessions</c>
/// endpoint. Asserts the SDK issued a well-formed request (merchantAccount, reference, amount in
/// minor units, returnUrl, <c>x-API-key</c> header) and that the canned 201 response maps to our
/// <see cref="Mach.Application.Dtos.PaymentSessionDto"/>.
/// </summary>
public sealed class SessionCreationTests : IDisposable
{
    private readonly WireMockServer _server = WireMockServer.Start();

    [Fact]
    public async Task CreatePaymentSession_drives_sdk_and_maps_response()
    {
        // Adyen's /sessions returns 201 Created on success.
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
            .Given(Request.Create().WithPath("/sessions").UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(201)
                .WithHeader("Content-Type", "application/json")
                .WithBody(cannedResponse));

        var gateway = BuildGateway();

        var result = await gateway.CreatePaymentSessionAsync(
            new CartId("cart-777"), new Money(49.99m, "EUR"), CancellationToken.None);

        // Response mapped to our DTO.
        result.IsSuccess.ShouldBeTrue(result.IsFailure ? result.Error.Message : null);
        result.Value.SessionId.ShouldBe("CS-SESSION-123");
        result.Value.SessionData.ShouldBe("Ab02b4c0!BQABAgABCDEF...");
        result.Value.CartId.Value.ShouldBe("cart-777");
        result.Value.Amount.Currency.ShouldBe("EUR");
        result.Value.Amount.Amount.ShouldBe(49.99m);

        // The SDK issued a well-formed outbound request.
        var log = _server.LogEntries.Single();
        var request = log.RequestMessage.ShouldNotBeNull();
        var headers = request.Headers.ShouldNotBeNull();

        // The Adyen SDK emits the API-key header as "X-API-Key"; match case-insensitively.
        var apiKeyHeader = headers
            .First(h => string.Equals(h.Key, "x-API-key", StringComparison.OrdinalIgnoreCase));
        apiKeyHeader.Value.ShouldContain("test-api-key");

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
            .Given(Request.Create().WithPath("/sessions").UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(401)
                .WithBody("""{"status":401,"errorCode":"171","message":"Unauthorized"}"""));

        var gateway = BuildGateway();

        var result = await gateway.CreatePaymentSessionAsync(
            new CartId("cart-1"), new Money(10m, "EUR"), CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("unexpected");
    }

    /// <summary>
    /// Builds an <see cref="IPaymentGateway"/> via the production <c>AddAdyen</c> DI wiring, but
    /// uses the SDK's own HttpClient configuration hook to redirect its base address to WireMock.
    /// No hand-rolled REST client is involved — the SDK's typed checkout service makes the call.
    /// </summary>
    private IPaymentGateway BuildGateway()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Adyen:ApiKey"] = "test-api-key",
                ["Adyen:MerchantAccount"] = "TestMerchant",
                ["Adyen:HmacKey"] = "00",
                ["Adyen:Environment"] = "Test",
                ["Adyen:ReturnUrl"] = "https://shop.example/return",
            })
            .Build();

        var services = new ServiceCollection();
        services.AddAdyen(config, configureHttpClient: http =>
        {
            // Point the Adyen SDK's checkout HttpClient at the WireMock server.
            http.BaseAddress = new Uri(_server.Url!);
        });

        var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<IPaymentGateway>();
    }

    public void Dispose() => _server.Dispose();
}
