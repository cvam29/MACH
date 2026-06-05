namespace Mach.Domain.ValueObjects;

/// <summary>
/// A product stock-keeping unit. Immutable value object.
/// </summary>
public readonly record struct Sku(string Value)
{
    public override string ToString() => Value;
}
