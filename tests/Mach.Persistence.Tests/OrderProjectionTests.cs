using Mach.Application.Dtos;
using Mach.Domain;
using Mach.Domain.ValueObjects;
using Mach.Persistence.Repositories;
using Shouldly;
using Xunit;
using Xunit.Abstractions;

namespace Mach.Persistence.Tests;

[Collection(SqlServerCollection.Name)]
public sealed class OrderProjectionTests(SqlServerFixture fixture, ITestOutputHelper output)
{
    private static OrderDto SampleOrder(string orderId, string customerId, OrderStatus status, decimal total) =>
        new(
            new OrderId(orderId),
            $"NUM-{orderId}",
            new CustomerId(customerId),
            status,
            PaymentStatus.Authorized,
            new Money(total, "EUR"),
            [
                new OrderLineDto(new Sku("SKU-1"), "Widget", 2, new Money(10m, "EUR"), new Money(20m, "EUR")),
                new OrderLineDto(new Sku("SKU-2"), "Gadget", 1, new Money(5m, "EUR"), new Money(5m, "EUR")),
            ],
            ShippingAddress: null,
            DeliveryType: null,
            CreatedAt: DateTimeOffset.UtcNow);

    [Fact]
    public async Task Upsert_inserts_then_GetById_round_trips()
    {
        if (!fixture.SkipIfUnavailable(output))
        {
            return;
        }

        var orderId = $"ord-{Guid.NewGuid():N}";
        var customerId = $"cust-{Guid.NewGuid():N}";

        await using var db = fixture.CreateContext();
        var store = new OrderProjectionStore(db, TimeProvider.System);
        await store.UpsertAsync(SampleOrder(orderId, customerId, OrderStatus.Paid, 25m), CancellationToken.None);

        await using var readDb = fixture.CreateContext();
        var readStore = new OrderProjectionStore(readDb, TimeProvider.System);
        var fetched = await readStore.GetByIdAsync(new OrderId(orderId), CancellationToken.None);

        fetched.ShouldNotBeNull();
        fetched!.OrderNumber.ShouldBe($"NUM-{orderId}");
        fetched.Status.ShouldBe(OrderStatus.Paid);
        fetched.PaymentStatus.ShouldBe(PaymentStatus.Authorized);
        fetched.TotalPrice.Amount.ShouldBe(25m);
        fetched.TotalPrice.Currency.ShouldBe("EUR");
        fetched.Lines.Count.ShouldBe(2);
        fetched.Lines.ShouldContain(l => l.Sku.Value == "SKU-1" && l.Quantity == 2);
    }

    [Fact]
    public async Task Upsert_updates_existing_and_replaces_lines()
    {
        if (!fixture.SkipIfUnavailable(output))
        {
            return;
        }

        var orderId = $"ord-{Guid.NewGuid():N}";
        var customerId = $"cust-{Guid.NewGuid():N}";

        await using var db = fixture.CreateContext();
        var store = new OrderProjectionStore(db, TimeProvider.System);
        await store.UpsertAsync(SampleOrder(orderId, customerId, OrderStatus.Pending, 25m), CancellationToken.None);

        // Update: status changes to Shipped, single replaced line.
        var updated = new OrderDto(
            new OrderId(orderId),
            $"NUM-{orderId}",
            new CustomerId(customerId),
            OrderStatus.Shipped,
            PaymentStatus.Captured,
            new Money(99m, "EUR"),
            [new OrderLineDto(new Sku("SKU-NEW"), "Replacement", 3, new Money(33m, "EUR"), new Money(99m, "EUR"))],
            ShippingAddress: null,
            DeliveryType: null,
            CreatedAt: DateTimeOffset.UtcNow);

        await using var db2 = fixture.CreateContext();
        var store2 = new OrderProjectionStore(db2, TimeProvider.System);
        await store2.UpsertAsync(updated, CancellationToken.None);

        await using var readDb = fixture.CreateContext();
        var readStore = new OrderProjectionStore(readDb, TimeProvider.System);
        var fetched = await readStore.GetByIdAsync(new OrderId(orderId), CancellationToken.None);

        fetched.ShouldNotBeNull();
        fetched!.Status.ShouldBe(OrderStatus.Shipped);
        fetched.PaymentStatus.ShouldBe(PaymentStatus.Captured);
        fetched.TotalPrice.Amount.ShouldBe(99m);
        fetched.Lines.Count.ShouldBe(1);
        fetched.Lines[0].Sku.Value.ShouldBe("SKU-NEW");
    }

    [Fact]
    public async Task GetByCustomer_returns_only_that_customers_orders()
    {
        if (!fixture.SkipIfUnavailable(output))
        {
            return;
        }

        var customerId = $"cust-{Guid.NewGuid():N}";
        var other = $"cust-{Guid.NewGuid():N}";

        await using var db = fixture.CreateContext();
        var store = new OrderProjectionStore(db, TimeProvider.System);
        await store.UpsertAsync(SampleOrder($"ord-{Guid.NewGuid():N}", customerId, OrderStatus.Paid, 10m), CancellationToken.None);
        await store.UpsertAsync(SampleOrder($"ord-{Guid.NewGuid():N}", customerId, OrderStatus.Paid, 20m), CancellationToken.None);
        await store.UpsertAsync(SampleOrder($"ord-{Guid.NewGuid():N}", other, OrderStatus.Paid, 30m), CancellationToken.None);

        await using var readDb = fixture.CreateContext();
        var readStore = new OrderProjectionStore(readDb, TimeProvider.System);
        var mine = await readStore.GetByCustomerAsync(new CustomerId(customerId), CancellationToken.None);

        mine.Count.ShouldBe(2);
        mine.ShouldAllBe(o => o.CustomerId!.Value.Value == customerId);
    }
}
