namespace Mach.Infrastructure.Maps.Tests;

using Mach.Application.Ports;
using Mach.Domain.ValueObjects;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;
using Xunit;

public sealed class AzureMapsGeoLocatorTests : IDisposable
{
    private const string SubscriptionKey = "test-key-123";

    // Canned Azure Maps "Get Geocoding" GeoJSON response.
    // GeoJSON coordinate order is [longitude, latitude].
    private const string CannedGeocodingJson = """
        {
          "type": "FeatureCollection",
          "features": [
            {
              "type": "Feature",
              "geometry": {
                "type": "Point",
                "coordinates": [13.4050, 52.5200]
              },
              "properties": {
                "type": "Address",
                "confidence": "High"
              }
            }
          ]
        }
        """;

    private readonly WireMockServer _server = WireMockServer.Start();

    [Fact]
    public async Task Geocode_ParsesLatLng_FromFirstFeature()
    {
        _server
            .Given(Request.Create().WithPath("/geocode").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody(CannedGeocodingJson));

        var locator = BuildLocator();

        var result = await locator.GeocodeAsync(BerlinAddress(), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Lat.ShouldBe(52.5200, tolerance: 1e-9);
        result.Value.Lng.ShouldBe(13.4050, tolerance: 1e-9);
    }

    [Fact]
    public async Task Geocode_HitsGeocodeEndpoint_WithKeyAndQuery()
    {
        _server
            .Given(Request.Create().WithPath("/geocode").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(200).WithBody(CannedGeocodingJson));

        var locator = BuildLocator();

        await locator.GeocodeAsync(BerlinAddress(), CancellationToken.None);

        var entry = _server.LogEntries.ShouldHaveSingleItem();
        var request = entry.RequestMessage;
        request.ShouldNotBeNull();

        var path = request.Path;
        path.ShouldNotBeNull();
        path.ShouldBe("/geocode");

        var query = request.Query;
        query.ShouldNotBeNull();
        query.ShouldContainKey("api-version");
        query["query"].ToString().ShouldContain("Berlin");

        // The subscription key must travel as the documented header.
        var headers = request.Headers;
        headers.ShouldNotBeNull();
        headers.ShouldContainKey("subscription-key");
        headers["subscription-key"].ToString().ShouldContain(SubscriptionKey);
    }

    [Fact]
    public async Task Geocode_ReturnsNotFound_WhenNoFeatures()
    {
        _server
            .Given(Request.Create().WithPath("/geocode").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithBody("""{ "type": "FeatureCollection", "features": [] }"""));

        var locator = BuildLocator();

        var result = await locator.GeocodeAsync(BerlinAddress(), CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("not_found");
    }

    [Fact]
    public async Task Geocode_ReturnsFailure_OnServerError()
    {
        _server
            .Given(Request.Create().WithPath("/geocode").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(500));

        var locator = BuildLocator();

        var result = await locator.GeocodeAsync(BerlinAddress(), CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
    }

    [Fact]
    public void AddMaps_RegistersStub_ByDefault()
    {
        var services = new ServiceCollection();
        var config = new ConfigurationBuilder().Build();

        services.AddMaps(config);

        using var provider = services.BuildServiceProvider();
        provider.GetRequiredService<IGeoLocator>().ShouldBeOfType<StubGeoLocator>();
    }

    [Fact]
    public void AddMaps_RegistersAzure_WhenConfigured()
    {
        var services = new ServiceCollection();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Maps:Provider"] = "Azure",
                ["Maps:SubscriptionKey"] = SubscriptionKey,
                ["Maps:BaseUrl"] = "https://atlas.example/",
            })
            .Build();

        services.AddMaps(config);

        using var provider = services.BuildServiceProvider();
        provider.GetRequiredService<IGeoLocator>().ShouldBeOfType<AzureMapsGeoLocator>();
    }

    public void Dispose() => _server.Stop();

    private IGeoLocator BuildLocator()
    {
        var services = new ServiceCollection();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Maps:Provider"] = "Azure",
                ["Maps:SubscriptionKey"] = SubscriptionKey,
                ["Maps:BaseUrl"] = _server.Url!,
            })
            .Build();

        services.AddMaps(config);

        var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<IGeoLocator>();
    }

    private static Address BerlinAddress() => new(
        Street: "Unter den Linden 1",
        City: "Berlin",
        PostalCode: "10117",
        Country: "DE");
}
