using Mach.Application.Dtos;
using Mach.Domain.ValueObjects;
using Mach.Infrastructure.Algolia;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Shouldly;

using AppSearchClient = Mach.Application.Ports.ISearchClient;

namespace Mach.Infrastructure.Algolia.Tests;

public class AlgoliaSearchClientValidationTests
{
    private static AlgoliaSearchClient Adapter()
    {
        var options = Options.Create(new AlgoliaOptions
        {
            AppId = "APP",
            AdminApiKey = "KEY",
            IndexName = "products",
        });

        // The underlying client is never invoked for input-validation failures, so a real
        // (un-contacted) client is fine here.
        var config = AlgoliaServiceCollectionExtensions.BuildConfig(options.Value);
        var algolia = new global::Algolia.Search.Clients.SearchClient(config);

        return new AlgoliaSearchClient(algolia, options, NullLogger<AlgoliaSearchClient>.Instance);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public async Task PartialUpdate_BlankObjectId_FailsValidation(string objectId)
    {
        var result = await Adapter().PartialUpdateProductAsync(
            objectId, new Dictionary<string, object?> { ["x"] = 1 }, CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("validation");
    }

    [Fact]
    public async Task PartialUpdate_EmptyChanges_FailsValidation()
    {
        var result = await Adapter().PartialUpdateProductAsync(
            "id", new Dictionary<string, object?>(), CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("validation");
    }

    [Fact]
    public async Task Delete_BlankObjectId_FailsValidation()
    {
        var result = await Adapter().DeleteProductAsync("  ", CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("validation");
    }

    [Fact]
    public async Task IndexProduct_InvalidRecord_FailsValidationWithoutNetwork()
    {
        var bad = new SearchRecord(
            ObjectId: "",
            Sku: new Sku("s"),
            Slug: "s",
            Name: "n",
            Description: "d",
            Price: new Money(1m, "EUR"),
            Categories: [],
            Facets: new Dictionary<string, string>(),
            InStock: true);

        var result = await Adapter().IndexProductAsync(bad, CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("validation");
    }

    [Fact]
    public void AddAlgolia_RegistersAdapterAsPort()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Algolia:AppId"] = "APP",
                ["Algolia:AdminApiKey"] = "KEY",
                ["Algolia:IndexName"] = "products",
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAlgolia(config);

        using var provider = services.BuildServiceProvider();

        var port = provider.GetService<AppSearchClient>();
        port.ShouldBeOfType<AlgoliaSearchClient>();
    }

    [Fact]
    public void AddAlgolia_MissingRequiredConfig_FailsOnResolution()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Algolia:AppId"] = "APP",
                // AdminApiKey and IndexName intentionally omitted.
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAlgolia(config);

        using var provider = services.BuildServiceProvider();

        Should.Throw<OptionsValidationException>(() => provider.GetRequiredService<AppSearchClient>());
    }
}
