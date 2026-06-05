namespace Mach.Domain.ValueObjects;

/// <summary>
/// A fulfilling store / warehouse used for distance-based delivery and as a
/// store / reception notification recipient.
/// </summary>
public readonly record struct StoreLocation(
    Guid Id,
    string Name,
    GeoPoint Location,
    string Email,
    string ReceptionEmail);
