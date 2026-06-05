using Mach.Contracts;
using Shouldly;

namespace Mach.Webhooks.Functions.Tests;

/// <summary>Pure mapping tests for the commercetools and Contentstack subscription payloads.</summary>
public sealed class WebhookMappingTests
{
    // --- commercetools ---------------------------------------------------------------------------

    [Fact]
    public void Commercetools_maps_product_message_to_product_changed()
    {
        const string body = """
            { "type": "ProductCreated", "resource": { "typeId": "product", "id": "prod-1" }, "slug": "tee" }
            """;

        CommercetoolsWebhookFunctions.TryMap(body, out var evt).ShouldBeTrue();
        evt.ProductId.ShouldBe("prod-1");
        evt.Slug.ShouldBe("tee");
        evt.Kind.ShouldBe(ProductChangeKind.Created);
    }

    [Theory]
    [InlineData("ProductDeleted", ProductChangeKind.Deleted)]
    [InlineData("ResourceDeleted", ProductChangeKind.Deleted)]
    [InlineData("ProductPublished", ProductChangeKind.Updated)]
    [InlineData("SomethingElse", ProductChangeKind.Updated)]
    public void Commercetools_maps_message_type_to_change_kind(string type, ProductChangeKind expected)
    {
        var body = $$"""
            { "type": "{{type}}", "resource": { "typeId": "product", "id": "p" } }
            """;

        CommercetoolsWebhookFunctions.TryMap(body, out var evt).ShouldBeTrue();
        evt.Kind.ShouldBe(expected);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not json")]
    [InlineData("{ \"resource\": { \"typeId\": \"order\", \"id\": \"o-1\" } }")] // non-product resource
    [InlineData("{ \"resource\": { \"typeId\": \"product\" } }")]               // missing id
    public void Commercetools_rejects_unusable_payloads(string body)
        => CommercetoolsWebhookFunctions.TryMap(body, out _).ShouldBeFalse();

    [Fact]
    public void Commercetools_falls_back_to_resourceId_when_resource_id_absent()
    {
        const string body = """
            { "type": "ProductUpdated", "resource": { "typeId": "product" }, "resourceId": "fallback-id" }
            """;

        CommercetoolsWebhookFunctions.TryMap(body, out var evt).ShouldBeTrue();
        evt.ProductId.ShouldBe("fallback-id");
    }

    // --- Contentstack ----------------------------------------------------------------------------

    [Fact]
    public void Contentstack_maps_publish_event_to_content_changed()
    {
        const string body = """
            {
              "event": "publish",
              "data": {
                "content_type": { "uid": "home_hero", "title": "Home Hero" },
                "entry": { "url": "/hero", "uid": "e1" }
              }
            }
            """;

        ContentstackWebhookFunctions.TryMap(body, out var evt).ShouldBeTrue();
        evt.ContentType.ShouldBe("home_hero");
        evt.Slug.ShouldBe("/hero");
        evt.Kind.ShouldBe(ContentChangeKind.Published);
    }

    [Theory]
    [InlineData("publish", ContentChangeKind.Published)]
    [InlineData("entry.unpublish", ContentChangeKind.Unpublished)]
    [InlineData("entry_delete", ContentChangeKind.Deleted)]
    [InlineData("mystery", ContentChangeKind.Published)]
    public void Contentstack_maps_event_name_to_change_kind(string eventName, ContentChangeKind expected)
    {
        var body = $$"""
            { "event": "{{eventName}}", "data": { "content_type": { "uid": "t" }, "entry": { "uid": "e" } } }
            """;

        ContentstackWebhookFunctions.TryMap(body, out var evt).ShouldBeTrue();
        evt.Kind.ShouldBe(expected);
    }

    [Fact]
    public void Contentstack_uses_unknown_content_type_when_absent()
    {
        const string body = """{ "event": "publish", "data": { "entry": { "uid": "e1" } } }""";

        ContentstackWebhookFunctions.TryMap(body, out var evt).ShouldBeTrue();
        evt.ContentType.ShouldBe("unknown");
        evt.Slug.ShouldBe("e1");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("}{ broken")]
    public void Contentstack_rejects_empty_or_invalid_json(string body)
        => ContentstackWebhookFunctions.TryMap(body, out _).ShouldBeFalse();
}
