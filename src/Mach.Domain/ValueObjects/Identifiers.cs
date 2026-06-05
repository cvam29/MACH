namespace Mach.Domain.ValueObjects;

/// <summary>Strongly-typed identifier for a commercetools cart.</summary>
public readonly record struct CartId(string Value)
{
    public override string ToString() => Value;
}

/// <summary>Strongly-typed identifier for an order.</summary>
public readonly record struct OrderId(string Value)
{
    public override string ToString() => Value;
}

/// <summary>Strongly-typed identifier for a customer.</summary>
public readonly record struct CustomerId(string Value)
{
    public override string ToString() => Value;
}

/// <summary>Strongly-typed identifier for a product.</summary>
public readonly record struct ProductId(string Value)
{
    public override string ToString() => Value;
}
