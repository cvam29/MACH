namespace Mach.Infrastructure.Maps.Tests;

using Mach.Domain.ValueObjects;
using Shouldly;
using Xunit;

public sealed class StubGeoLocatorTests
{
    private readonly StubGeoLocator _sut = new();

    private static Address BerlinAddress() => new(
        Street: "Unter den Linden 1",
        City: "Berlin",
        PostalCode: "10117",
        Country: "DE");

    private static Address MunichAddress() => new(
        Street: "Marienplatz 8",
        City: "Munich",
        PostalCode: "80331",
        Country: "DE");

    [Fact]
    public async Task SameAddress_YieldsSamePoint()
    {
        var a = (await _sut.GeocodeAsync(BerlinAddress(), default)).Value;
        var b = (await _sut.GeocodeAsync(BerlinAddress(), default)).Value;

        b.ShouldBe(a);
    }

    [Fact]
    public async Task DifferentCities_YieldDifferentPoints()
    {
        var berlin = (await _sut.GeocodeAsync(BerlinAddress(), default)).Value;
        var munich = (await _sut.GeocodeAsync(MunichAddress(), default)).Value;

        munich.ShouldNotBe(berlin);
    }

    [Fact]
    public async Task GeneratedPoint_IsWithinPlausibleRegion()
    {
        var point = (await _sut.GeocodeAsync(BerlinAddress(), default)).Value;

        point.Lat.ShouldBeInRange(43.0, 55.0);
        point.Lng.ShouldBeInRange(2.0, 24.0);
    }

    [Fact]
    public async Task Geocode_AlwaysSucceeds()
    {
        var result = await _sut.GeocodeAsync(BerlinAddress(), default);

        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task DistanceKm_MatchesHaversineHelper()
    {
        var berlin = (await _sut.GeocodeAsync(BerlinAddress(), default)).Value;
        var munich = (await _sut.GeocodeAsync(MunichAddress(), default)).Value;

        var distance = (await _sut.DistanceKmAsync(berlin, munich, default)).Value;

        distance.ShouldBe(Haversine.DistanceKm(berlin, munich), tolerance: 1e-9);
    }
}
