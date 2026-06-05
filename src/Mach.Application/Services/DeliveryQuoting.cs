using Mach.Application.Ports;
using Mach.Domain;
using Mach.Domain.ValueObjects;
using Microsoft.Extensions.Options;

namespace Mach.Application.Services;

/// <summary>
/// Pure distance-based delivery quoting. Finds the nearest fulfilling store via
/// <see cref="IGeoLocator"/> and prices each delivery type as <c>base + perKm * distance</c>.
/// Same-day is gated by a distance threshold; store pickup is free.
/// </summary>
public sealed class DeliveryQuoting : IDeliveryQuoting
{
    private readonly IGeoLocator _geo;
    private readonly DeliveryQuotingOptions _options;

    public DeliveryQuoting(IGeoLocator geo, IOptions<DeliveryQuotingOptions> options)
    {
        _geo = geo;
        _options = options.Value;
    }

    public async Task<IReadOnlyList<DeliveryQuote>> QuoteAsync(
        Money cartTotal,
        Address destination,
        IReadOnlyList<StoreLocation> stores,
        CancellationToken ct)
    {
        var currency = _options.Currency;

        // Store pickup is always available and free, independent of geocoding.
        var pickup = new DeliveryQuote(
            DeliveryType.StorePickup,
            Money.Zero(currency),
            _options.StorePickupEtaHours,
            Available: stores.Count > 0);

        if (stores.Count == 0)
        {
            return [Unavailable(DeliveryType.Standard, currency),
                    Unavailable(DeliveryType.Express, currency),
                    Unavailable(DeliveryType.SameDay, currency),
                    pickup];
        }

        var geocode = await _geo.GeocodeAsync(destination, ct).ConfigureAwait(false);
        if (geocode.IsFailure)
        {
            return [Unavailable(DeliveryType.Standard, currency),
                    Unavailable(DeliveryType.Express, currency),
                    Unavailable(DeliveryType.SameDay, currency),
                    pickup];
        }

        var destinationPoint = geocode.Value;
        var nearestKm = double.MaxValue;
        foreach (var store in stores)
        {
            var distance = await _geo.DistanceKmAsync(store.Location, destinationPoint, ct)
                .ConfigureAwait(false);
            if (distance.IsSuccess && distance.Value < nearestKm)
            {
                nearestKm = distance.Value;
            }
        }

        if (double.IsPositiveInfinity(nearestKm) || nearestKm == double.MaxValue)
        {
            return [Unavailable(DeliveryType.Standard, currency),
                    Unavailable(DeliveryType.Express, currency),
                    Unavailable(DeliveryType.SameDay, currency),
                    pickup];
        }

        var standard = Price(
            DeliveryType.Standard, _options.StandardBaseFee, _options.StandardPerKm,
            _options.StandardEtaHours, nearestKm, currency, available: true);

        var express = Price(
            DeliveryType.Express, _options.ExpressBaseFee, _options.ExpressPerKm,
            _options.ExpressEtaHours, nearestKm, currency, available: true);

        var sameDayAvailable = nearestKm <= _options.SameDayMaxDistanceKm;
        var sameDay = Price(
            DeliveryType.SameDay, _options.SameDayBaseFee, _options.SameDayPerKm,
            _options.SameDayEtaHours, nearestKm, currency, sameDayAvailable);

        return [standard, express, sameDay, pickup];
    }

    private static DeliveryQuote Price(
        DeliveryType type, decimal baseFee, decimal perKm, int etaHours,
        double distanceKm, string currency, bool available)
    {
        var amount = baseFee + (perKm * (decimal)distanceKm);
        return new DeliveryQuote(type, new Money(decimal.Round(amount, 2), currency), etaHours, available);
    }

    private static DeliveryQuote Unavailable(DeliveryType type, string currency)
        => new(type, Money.Zero(currency), 0, Available: false);
}
