namespace Mach.Application.Services;

/// <summary>
/// Tunable rules for distance-based delivery pricing/ETAs.
/// Prices are <c>BaseFee + PerKm * distance</c>; same-day is gated by <see cref="SameDayMaxDistanceKm"/>.
/// </summary>
public sealed class DeliveryQuotingOptions
{
    public const string SectionName = "Delivery";

    /// <summary>ISO-4217 currency for the computed delivery prices.</summary>
    public string Currency { get; set; } = "EUR";

    public decimal StandardBaseFee { get; set; } = 4.99m;

    public decimal StandardPerKm { get; set; } = 0.10m;

    public int StandardEtaHours { get; set; } = 72;

    public decimal ExpressBaseFee { get; set; } = 9.99m;

    public decimal ExpressPerKm { get; set; } = 0.25m;

    public int ExpressEtaHours { get; set; } = 24;

    public decimal SameDayBaseFee { get; set; } = 14.99m;

    public decimal SameDayPerKm { get; set; } = 0.50m;

    public int SameDayEtaHours { get; set; } = 6;

    /// <summary>Same-day delivery is only offered within this distance of a store.</summary>
    public double SameDayMaxDistanceKm { get; set; } = 20.0;

    public int StorePickupEtaHours { get; set; } = 2;
}
