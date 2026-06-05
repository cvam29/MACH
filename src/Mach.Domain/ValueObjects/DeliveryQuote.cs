namespace Mach.Domain.ValueObjects;

/// <summary>
/// A priced delivery option for a particular <see cref="DeliveryType"/>, with an ETA in hours.
/// </summary>
public readonly record struct DeliveryQuote(
    DeliveryType Type,
    Money Price,
    int EtaHours,
    bool Available);
