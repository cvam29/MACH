using Mach.Application.Ports;
using Mach.Application.Services;
using Mach.Domain;
using Mach.Domain.ValueObjects;
using Microsoft.Extensions.Options;
using Shouldly;

namespace Mach.Application.Tests;

public class DeliveryQuotingTests
{
    private static readonly Address Destination = new("1 High St", "Town", "AB1 2CD", "GB");

    private static readonly StoreLocation Store = new(
        Guid.NewGuid(), "Central", new GeoPoint(51.5, -0.12), "store@x.test", "reception@x.test");

    private static DeliveryQuoting Build(double distanceKm, DeliveryQuotingOptions? options = null)
    {
        var geo = new FakeGeoLocator(new GeoPoint(51.5, -0.13), distanceKm);
        return new DeliveryQuoting(geo, Options.Create(options ?? new DeliveryQuotingOptions()));
    }

    [Fact]
    public async Task Quote_PricesScaleWithDistance()
    {
        var near = await Build(5).QuoteAsync(new Money(50m, "EUR"), Destination, [Store], default);
        var far = await Build(50).QuoteAsync(new Money(50m, "EUR"), Destination, [Store], default);

        var nearStandard = near.Single(q => q.Type == DeliveryType.Standard);
        var farStandard = far.Single(q => q.Type == DeliveryType.Standard);

        farStandard.Price.Amount.ShouldBeGreaterThan(nearStandard.Price.Amount);
    }

    [Fact]
    public async Task Quote_SameDay_DisabledBeyondThreshold()
    {
        var options = new DeliveryQuotingOptions { SameDayMaxDistanceKm = 20 };

        var inside = await Build(10, options).QuoteAsync(new Money(50m, "EUR"), Destination, [Store], default);
        var outside = await Build(40, options).QuoteAsync(new Money(50m, "EUR"), Destination, [Store], default);

        inside.Single(q => q.Type == DeliveryType.SameDay).Available.ShouldBeTrue();
        outside.Single(q => q.Type == DeliveryType.SameDay).Available.ShouldBeFalse();
    }

    [Fact]
    public async Task Quote_StorePickup_IsFreeAndAvailable()
    {
        var quotes = await Build(15).QuoteAsync(new Money(50m, "EUR"), Destination, [Store], default);

        var pickup = quotes.Single(q => q.Type == DeliveryType.StorePickup);
        pickup.Available.ShouldBeTrue();
        pickup.Price.Amount.ShouldBe(0m);
    }

    [Fact]
    public async Task Quote_NoStores_OnlyPickupSlotPresentButUnavailable()
    {
        var quotes = await Build(15).QuoteAsync(new Money(50m, "EUR"), Destination, [], default);

        quotes.Single(q => q.Type == DeliveryType.StorePickup).Available.ShouldBeFalse();
        quotes.Single(q => q.Type == DeliveryType.Standard).Available.ShouldBeFalse();
    }

    private sealed class FakeGeoLocator(GeoPoint geocodeResult, double distanceKm) : IGeoLocator
    {
        public Task<Result<GeoPoint>> GeocodeAsync(Address address, CancellationToken ct)
            => Task.FromResult(Result.Success(geocodeResult));

        public Task<Result<double>> DistanceKmAsync(GeoPoint from, GeoPoint to, CancellationToken ct)
            => Task.FromResult(Result.Success(distanceKm));
    }
}
