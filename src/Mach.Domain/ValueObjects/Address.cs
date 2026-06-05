namespace Mach.Domain.ValueObjects;

/// <summary>
/// A postal address used for shipping/billing and geocoding.
/// </summary>
public readonly record struct Address(
    string Street,
    string City,
    string PostalCode,
    string Country,
    string? State = null,
    string? FirstName = null,
    string? LastName = null);
