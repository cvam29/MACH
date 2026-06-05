using System.Net;
using System.Text.Json;

using Algolia.Search.Clients;

using Mach.Application.Dtos;
using Mach.Domain;
using Mach.Domain.ValueObjects;
using Mach.Infrastructure.Algolia;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Shouldly;

using WireMock;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

using AppSearchClient = Mach.Application.Ports.ISearchClient;

namespace Mach.Infrastructure.Algolia.Tests;

/// <summary>
/// Exercises the adapter against a WireMock transport. The Algolia client is pointed at the mock
/// host via <c>CustomHosts</c>, so we can assert the exact HTTP request shaping for each operation.
/// </summary>
public sealed class AlgoliaSearchClientWireMockTests : IDisposable
{
    private const string IndexName = "products";
    private readonly WireMockServer _server;

    public AlgoliaSearchClientWireMockTests()
    {
        _server = WireMockServer.Start();

        // Generic success responses for every Algolia write endpoint we hit. Bodies carry the
        // minimal fields the client deserializes.
        _server
            .Given(Request.Create().WithPath("/1/indexes/*").UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.OK)
                .WithHeader("Content-Type", "application/json")
                .WithBody("{\"taskID\":1,\"objectID\":\"prod-1\",\"updatedAt\":\"2026-01-01T00:00:00.000Z\",\"createdAt\":\"2026-01-01T00:00:00.000Z\"}"));

        _server
            .Given(Request.Create().WithPath("/1/indexes/*").UsingPut())
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.OK)
                .WithHeader("Content-Type", "application/json")
                .WithBody("{\"taskID\":1,\"updatedAt\":\"2026-01-01T00:00:00.000Z\"}"));

        _server
            .Given(Request.Create().WithPath("/1/indexes/*").UsingDelete())
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.OK)
                .WithHeader("Content-Type", "application/json")
                .WithBody("{\"taskID\":1,\"deletedAt\":\"2026-01-01T00:00:00.000Z\"}"));

        // task polling (used by ReplaceAllObjects waits)
        _server
            .Given(Request.Create().WithPath("/1/indexes/*/task/*").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.OK)
                .WithHeader("Content-Type", "application/json")
                .WithBody("{\"status\":\"published\",\"pendingTask\":false}"));
    }

    private AppSearchClient CreateAdapter()
    {
        var options = new AlgoliaOptions
        {
            AppId = "TEST_APP",
            AdminApiKey = "TEST_KEY",
            IndexName = IndexName,
            CustomHosts = { _server.Url! },
            WriteTimeoutSeconds = 10,
        };

        var config = AlgoliaServiceCollectionExtensions.BuildConfig(options);
        var algolia = new SearchClient(config);

        return new AlgoliaSearchClient(
            algolia,
            Options.Create(options),
            NullLogger<AlgoliaSearchClient>.Instance);
    }

    private IReadOnlyList<RequestMessage> Requests()
        => _server.LogEntries
            .Select(e => e.RequestMessage)
            .Where(m => m is not null)
            .Cast<RequestMessage>()
            .ToList();

    private static SearchRecord Record(string objectId = "prod-1") => new(
        ObjectId: objectId,
        Sku: new Sku("SKU-1"),
        Slug: "slug-1",
        Name: "Product One",
        Description: "Desc",
        Price: new Money(10m, "EUR"),
        Categories: ["cat"],
        Facets: new Dictionary<string, string> { ["brand"] = "Acme" },
        InStock: true);

    [Fact]
    public async Task IndexProduct_PostsFlatRecordToIndex()
    {
        var adapter = CreateAdapter();

        var result = await adapter.IndexProductAsync(Record(), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();

        var entry = Requests().Single(m =>
            m.Method == "POST" &&
            m.Path == $"/1/indexes/{IndexName}");

        using var doc = JsonDocument.Parse(entry.Body ?? "{}");
        var root = doc.RootElement;
        root.GetProperty("objectID").GetString().ShouldBe("prod-1");
        root.GetProperty("name").GetString().ShouldBe("Product One");
        root.GetProperty("brand").GetString().ShouldBe("Acme");
        root.GetProperty("price").GetDecimal().ShouldBe(10m);
        root.GetProperty("inStock").GetBoolean().ShouldBeTrue();
    }

    [Fact]
    public async Task PartialUpdate_PostsToPartialEndpointWithObjectId()
    {
        var adapter = CreateAdapter();

        var changes = new Dictionary<string, object?> { ["price"] = 12.5m };
        var result = await adapter.PartialUpdateProductAsync("prod-1", changes, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();

        var entry = Requests().Single(m =>
            m.Method == "POST" &&
            m.Path == $"/1/indexes/{IndexName}/prod-1/partial");

        // createIfNotExists=false should be reflected in the query string.
        (entry.RawQuery ?? string.Empty).ShouldContain("createIfNotExists=false");

        using var doc = JsonDocument.Parse(entry.Body ?? "{}");
        doc.RootElement.GetProperty("price").GetDecimal().ShouldBe(12.5m);
        doc.RootElement.GetProperty("objectID").GetString().ShouldBe("prod-1");
    }

    [Fact]
    public async Task DeleteProduct_IssuesDeleteForObjectId()
    {
        var adapter = CreateAdapter();

        var result = await adapter.DeleteProductAsync("prod-1", CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();

        Requests().ShouldContain(m =>
            m.Method == "DELETE" &&
            m.Path == $"/1/indexes/{IndexName}/prod-1");
    }

    [Fact]
    public async Task FullReindex_AppliesSettingsThenReplacesObjects()
    {
        var adapter = CreateAdapter();

        var result = await adapter.FullReindexAsync([Record("a"), Record("b")], CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();

        // Settings applied via PUT /1/indexes/{index}/settings
        Requests().ShouldContain(m =>
            m.Method == "PUT" &&
            (m.Path ?? string.Empty).Contains("/settings"));

        // ReplaceAllObjects batches records (to a temporary index) via a /batch POST.
        Requests().ShouldContain(m =>
            m.Method == "POST" &&
            (m.Path ?? string.Empty).Contains("/batch"));
    }

    [Fact]
    public async Task ApiError_IsTranslatedToFailureResult()
    {
        _server.Reset();
        _server
            .Given(Request.Create().WithPath("/1/indexes/*").UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.InternalServerError)
                .WithHeader("Content-Type", "application/json")
                .WithBody("{\"message\":\"boom\"}"));

        var adapter = CreateAdapter();

        var result = await adapter.IndexProductAsync(Record(), CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("unexpected");
    }

    [Fact]
    public async Task NotFoundError_MapsToNotFound()
    {
        _server.Reset();
        _server
            .Given(Request.Create().WithPath("/1/indexes/*").UsingDelete())
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.NotFound)
                .WithHeader("Content-Type", "application/json")
                .WithBody("{\"message\":\"not found\"}"));

        var adapter = CreateAdapter();

        var result = await adapter.DeleteProductAsync("missing", CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("not_found");
    }

    public void Dispose() => _server.Dispose();
}
