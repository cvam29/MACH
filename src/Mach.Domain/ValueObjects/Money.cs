namespace Mach.Domain.ValueObjects;

/// <summary>
/// A monetary amount in a given ISO-4217 currency. Immutable value object.
/// </summary>
public readonly record struct Money(decimal Amount, string Currency)
{
    public static Money Zero(string currency) => new(0m, currency);

    public Money Add(Money other)
    {
        EnsureSameCurrency(other);
        return this with { Amount = Amount + other.Amount };
    }

    public Money Multiply(decimal factor) => this with { Amount = Amount * factor };

    private void EnsureSameCurrency(Money other)
    {
        if (!string.Equals(Currency, other.Currency, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Cannot operate on Money with mismatched currencies '{Currency}' and '{other.Currency}'.");
        }
    }

    public override string ToString() => $"{Amount:0.##} {Currency}";
}
