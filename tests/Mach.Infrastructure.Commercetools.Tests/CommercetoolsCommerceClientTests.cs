using commercetools.Sdk.Api.Client;

using Mach.Application.Dtos;
using Mach.Application.Ports;
using Mach.Domain.ValueObjects;
using Mach.Infrastructure.Commercetools;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using Shouldly;

using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace Mach.Infrastructure.Commercetools.Tests;

/// <summary>
/// Drives <see cref="CommercetoolsCommerceClient"/> end-to-end against a WireMock server standing in
/// for both the commercetools auth token endpoint and the HTTP API.
/// </summary>
public sealed class CommercetoolsCommerceClientTests : IDisposable
{
    private readonly WireMockServer _server;
    private readonly ServiceProvider _provider;
    private readonly ICommerceClient _client;

    public CommercetoolsCommerceClientTests()
    {
        _server = WireMockServer.Start();

        // Client-credentials token the SDK acquires before any API call.
        _server.Given(Request.Create().WithPath("/oauth/token").UsingPost())
            .RespondWith(Response.Create().WithStatusCode(200)
                .WithBody("""{"access_token":"svc-token","expires_in":3600,"scope":"manage_project:demo"}"""));

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Commercetools:ProjectKey"] = "demo",
                ["Commercetools:ClientId"] = "id",
                ["Commercetools:ClientSecret"] = "secret",
                ["Commercetools:ScopeList"] = "manage_project:demo",
                ["Commercetools:ApiUrl"] = _server.Url!,
                ["Commercetools:AuthUrl"] = _server.Url!,
                ["Commercetools:DefaultLocale"] = "en",
            })
            .Build();

        var services = new ServiceCollection();
        services.AddCommercetools(config);
        _provider = services.BuildServiceProvider();
        _client = _provider.GetRequiredService<ICommerceClient>();
    }

    [Fact]
    public async Task GetProductBySlugAsync_maps_projection_to_ProductDto()
    {
        _server.Given(Request.Create().WithPath("/demo/product-projections").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(200).WithHeader("Content-Type", "application/json")
                .WithBody("""
                {
                  "limit": 1, "offset": 0, "count": 1, "total": 1,
                  "results": [{
                    "id": "prod-1", "version": 1,
                    "createdAt": "2024-01-01T00:00:00.000Z", "lastModifiedAt": "2024-01-01T00:00:00.000Z",
                    "productType": { "typeId": "product-type", "id": "pt-1" },
                    "name": { "en": "Running Shoe" },
                    "slug": { "en": "running-shoe" },
                    "description": { "en": "Fast shoe." },
                    "categories": [{ "typeId": "category", "id": "cat-1" }],
                    "masterVariant": {
                      "id": 1, "sku": "SKU-1",
                      "prices": [{ "id": "p1", "value": { "type": "centPrecision", "currencyCode": "EUR", "centAmount": 8999, "fractionDigits": 2 } }],
                      "price": { "id": "p1", "value": { "type": "centPrecision", "currencyCode": "EUR", "centAmount": 8999, "fractionDigits": 2 } },
                      "images": [{ "url": "https://img/shoe.png", "dimensions": { "w": 100, "h": 100 } }],
                      "attributes": [{ "name": "color", "value": "black" }],
                      "availability": { "isOnStock": true, "availableQuantity": 5 }
                    },
                    "variants": [],
                    "published": true, "hasStagedChanges": false
                  }]
                }
                """));

        var result = await _client.GetProductBySlugAsync("running-shoe", CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        var product = result.Value;
        product.Id.Value.ShouldBe("prod-1");
        product.Slug.ShouldBe("running-shoe");
        product.Name.ShouldBe("Running Shoe");
        product.Variants.Count.ShouldBe(1);
        product.Variants[0].Sku.Value.ShouldBe("SKU-1");
        product.Variants[0].Price.Amount.ShouldBe(89.99m);
        product.Variants[0].Attributes["color"].ShouldBe("black");
        product.Variants[0].InStock.ShouldBeTrue();
    }

    [Fact]
    public async Task GetProductBySlugAsync_returns_not_found_when_no_results()
    {
        _server.Given(Request.Create().WithPath("/demo/product-projections").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(200)
                .WithBody("""{ "limit": 1, "offset": 0, "count": 0, "total": 0, "results": [] }"""));

        var result = await _client.GetProductBySlugAsync("missing", CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("not_found");
    }

    [Fact]
    public async Task AddLineItemAsync_posts_update_with_version_and_maps_cart()
    {
        _server.Given(Request.Create().WithPath("/demo/carts/cart-1").UsingPost())
            .RespondWith(Response.Create().WithStatusCode(200).WithHeader("Content-Type", "application/json")
                .WithBody("""
                {
                  "id": "cart-1", "version": 5,
                  "createdAt": "2024-01-01T00:00:00.000Z", "lastModifiedAt": "2024-01-01T00:00:00.000Z",
                  "lineItems": [{
                    "id": "li-1", "productId": "prod-1",
                    "name": { "en": "Shoe" },
                    "variant": { "id": 1, "sku": "SKU-1" },
                    "price": { "id": "p1", "value": { "type": "centPrecision", "currencyCode": "EUR", "centAmount": 1000, "fractionDigits": 2 } },
                    "quantity": 3,
                    "totalPrice": { "type": "centPrecision", "currencyCode": "EUR", "centAmount": 3000, "fractionDigits": 2 },
                    "discountedPricePerQuantity": [], "taxedPricePortions": [], "state": [], "perMethodTaxRate": []
                  }],
                  "customLineItems": [],
                  "totalPrice": { "type": "centPrecision", "currencyCode": "EUR", "centAmount": 3000, "fractionDigits": 2 },
                  "taxMode": "Platform", "taxRoundingMode": "HalfEven", "taxCalculationMode": "LineItemLevel",
                  "inventoryMode": "None", "cartState": "Active", "shippingMode": "Single",
                  "shipping": [], "itemShippingAddresses": [], "discountCodes": [], "directDiscounts": [],
                  "refusedGifts": [], "origin": "Customer"
                }
                """));

        var request = new AddLineItemRequest(new Sku("SKU-1"), 3);
        var result = await _client.AddLineItemAsync(new CartId("cart-1"), 4, request, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Id.Value.ShouldBe("cart-1");
        result.Value.Version.ShouldBe(5);
        result.Value.LineItems.Count.ShouldBe(1);
        result.Value.LineItems[0].Quantity.ShouldBe(3);
        result.Value.LineItems[0].TotalPrice.Amount.ShouldBe(30m);

        // The outgoing update body must echo the supplied optimistic-concurrency version.
        var body = _server.LogEntries
            .Last(e => e.RequestMessage!.Path == "/demo/carts/cart-1")
            .RequestMessage!.Body ?? string.Empty;
        body.ShouldContain("\"version\":4");
        body.ShouldContain("addLineItem");
    }

    [Fact]
    public async Task GetCartAsync_translates_404_to_not_found_result()
    {
        _server.Given(Request.Create().WithPath("/demo/carts/ghost").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(404)
                .WithBody("""{ "statusCode": 404, "message": "The Cart with ID 'ghost' was not found.", "errors": [] }"""));

        var result = await _client.GetCartAsync(new CartId("ghost"), CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("not_found");
    }

    [Fact]
    public async Task AddLineItemAsync_translates_409_to_conflict_result()
    {
        _server.Given(Request.Create().WithPath("/demo/carts/cart-9").UsingPost())
            .RespondWith(Response.Create().WithStatusCode(409)
                .WithBody("""{ "statusCode": 409, "message": "Object cart-9 has a different version.", "errors": [] }"""));

        var request = new AddLineItemRequest(new Sku("SKU-1"), 1);
        var result = await _client.AddLineItemAsync(new CartId("cart-9"), 1, request, CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("conflict");
    }

    public void Dispose()
    {
        _provider.Dispose();
        _server.Dispose();
    }
}
