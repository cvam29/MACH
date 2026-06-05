using Mach.Domain.ValueObjects;
using Shouldly;

namespace Mach.Domain.Tests;

public class MoneyTests
{
    [Fact]
    public void Add_SameCurrency_SumsAmounts()
    {
        var a = new Money(10m, "EUR");
        var b = new Money(5.50m, "EUR");

        a.Add(b).ShouldBe(new Money(15.50m, "EUR"));
    }

    [Fact]
    public void Add_DifferentCurrency_Throws()
    {
        var a = new Money(10m, "EUR");
        var b = new Money(5m, "USD");

        Should.Throw<InvalidOperationException>(() => a.Add(b));
    }

    [Fact]
    public void Multiply_ScalesAmount()
    {
        new Money(4m, "EUR").Multiply(3m).Amount.ShouldBe(12m);
    }

    [Fact]
    public void Zero_HasZeroAmount()
    {
        Money.Zero("GBP").ShouldBe(new Money(0m, "GBP"));
    }
}
