using Mach.Application.Ports;
using Mach.Domain;
using Mach.Infrastructure.Contentstack;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace Mach.Infrastructure.Contentstack.Tests;

public sealed class ContentstackCmsClientTests : IDisposable
{
    private const string ApiKey = "test_api_key";
    private const string DeliveryToken = "test_delivery_token";
    private const string Environment = "development";

    private readonly WireMockServer _server;
    private readonly ServiceProvider _provider;

    public ContentstackCmsClientTests()
    {
        _server = WireMockServer.Start();

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Contentstack:ApiKey"] = ApiKey,
                ["Contentstack:DeliveryToken"] = DeliveryToken,
                ["Contentstack:Environment"] = Environment,
                ["Contentstack:Locale"] = "en-us",
                ["Contentstack:BaseUrl"] = _server.Url,
            })
            .Build();

        var services = new ServiceCollection();
        services.AddContentstack(config);
        _provider = services.BuildServiceProvider();
    }

    private ICmsClient Client => _provider.GetRequiredService<ICmsClient>();

    [Fact]
    public async Task GetEntryAsync_maps_entry_and_sends_correct_request()
    {
        const string body = """
        {
          "entries": [
            {
              "uid": "blt123",
              "title": "Meet the Atlas Tee",
              "url": "/pdp/atlas-tee",
              "product_slug": "atlas-tee",
              "marketing_headline": "Built to move",
              "highlights": ["Cotton", "Acme"],
              "in_stock": true,
              "rating": 4
            }
          ]
        }
        """;

        _server
            .Given(Request.Create()
                .WithPath("/v3/content_types/pdp_marketing_block/entries")
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody(body));

        var result = await Client.GetEntryAsync("pdp_marketing_block", "pdp/atlas-tee", CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        var entry = result.Value;
        entry.ContentType.ShouldBe("pdp_marketing_block");
        entry.Slug.ShouldBe("/pdp/atlas-tee");
        entry.Title.ShouldBe("Meet the Atlas Tee");
        entry.Fields["product_slug"].ShouldBe("atlas-tee");
        entry.Fields["in_stock"].ShouldBe(true);
        entry.Fields["rating"].ShouldBe(4L);
        entry.Fields["highlights"].ShouldBeOfType<List<object?>>().Count.ShouldBe(2);

        var request = _server.LogEntries.ShouldHaveSingleItem();
        var message = request.RequestMessage!;
        var query = message.Query!;
        query["environment"]!.ShouldContain(Environment);
        query["locale"]!.ShouldContain("en-us");
        query["query"]!.Single().ShouldContain("\"url\":\"/pdp/atlas-tee\"");

        var headers = message.Headers!;
        headers["api_key"]!.ShouldContain(ApiKey);
        headers["access_token"]!.ShouldContain(DeliveryToken);
        headers["environment"]!.ShouldContain(Environment);
    }

    [Fact]
    public async Task GetNavigationAsync_maps_items_to_children()
    {
        const string body = """
        {
          "entries": [
            {
              "uid": "nav1",
              "title": "Main Navigation",
              "url": "/navigation/main",
              "items": [
                { "label": "Tops", "url": "/catalog/tops" },
                { "label": "Outerwear", "url": "/catalog/outerwear",
                  "children": [ { "label": "Jackets", "url": "/catalog/jackets" } ] }
              ]
            }
          ]
        }
        """;

        _server
            .Given(Request.Create()
                .WithPath("/v3/content_types/navigation/entries")
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody(body));

        var result = await Client.GetNavigationAsync(CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        var nav = result.Value;
        nav.Label.ShouldBe("Main Navigation");
        nav.Url.ShouldBe("/navigation/main");
        nav.Children.Count.ShouldBe(2);
        nav.Children[0].Label.ShouldBe("Tops");
        nav.Children[0].Url.ShouldBe("/catalog/tops");
        nav.Children[1].Children.ShouldHaveSingleItem().Label.ShouldBe("Jackets");
    }

    [Theory]
    [InlineData(NotificationAudience.Customer, "customer")]
    [InlineData(NotificationAudience.Store, "store")]
    [InlineData(NotificationAudience.Supplier, "supplier")]
    [InlineData(NotificationAudience.Reception, "reception")]
    public async Task GetEmailTemplateAsync_queries_by_audience_and_maps(
        NotificationAudience audience, string token)
    {
        var body = $$"""
        {
          "entries": [
            {
              "uid": "tpl_{{token}}",
              "title": "Email Template for {{token}}",
              "audience": "{{token}}",
              "subject": "Subject for {{token}}",
              "body": "Body for {{token}} with order token"
            }
          ]
        }
        """;

        _server
            .Given(Request.Create()
                .WithPath("/v3/content_types/email_template/entries")
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody(body));

        var result = await Client.GetEmailTemplateAsync(audience, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        var tpl = result.Value;
        tpl.Audience.ShouldBe(audience);
        tpl.Subject.ShouldBe($"Subject for {token}");
        tpl.HtmlBody.ShouldContain($"Body for {token}");

        var request = _server.LogEntries.ShouldHaveSingleItem();
        var query = request.RequestMessage!.Query!;
        query["query"]!.Single().ShouldContain($"\"audience\":\"{token}\"");
    }

    [Fact]
    public async Task GetEntryAsync_returns_not_found_when_no_entries()
    {
        _server
            .Given(Request.Create()
                .WithPath("/v3/content_types/pdp_marketing_block/entries")
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody("""{ "entries": [] }"""));

        var result = await Client.GetEntryAsync("pdp_marketing_block", "missing", CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("not_found");
    }

    [Fact]
    public async Task GetEntryAsync_returns_not_found_on_http_404()
    {
        _server
            .Given(Request.Create()
                .WithPath("/v3/content_types/pdp_marketing_block/entries")
                .UsingGet())
            .RespondWith(Response.Create().WithStatusCode(404));

        var result = await Client.GetEntryAsync("pdp_marketing_block", "missing", CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("not_found");
    }

    [Fact]
    public async Task GetNavigationAsync_returns_failure_on_server_error()
    {
        _server
            .Given(Request.Create()
                .WithPath("/v3/content_types/navigation/entries")
                .UsingGet())
            .RespondWith(Response.Create().WithStatusCode(500));

        var result = await Client.GetNavigationAsync(CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("unexpected");
    }

    [Fact]
    public async Task GetEntryAsync_validates_blank_arguments()
    {
        var result = await Client.GetEntryAsync("", "slug", CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("validation");
    }

    public void Dispose()
    {
        _provider.Dispose();
        _server.Stop();
        _server.Dispose();
    }
}
