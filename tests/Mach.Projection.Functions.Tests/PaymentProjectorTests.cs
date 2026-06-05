using Mach.Application.Dtos;
using Mach.Contracts;
using Mach.Domain;
using Mach.Domain.ValueObjects;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;

namespace Mach.Projection.Functions.Tests;

public sealed class PaymentProjectorTests
{
    private static OrderDto Order(string id, OrderStatus status, PaymentStatus payment) =>
        new(
            Id: new OrderId(id),
            OrderNumber: $"ON-{id}",
            CustomerId: new CustomerId("cust-1"),
            Status: status,
            PaymentStatus: payment,
            TotalPrice: new Money(100m, "EUR"),
            Lines: [],
            ShippingAddress: null,
            DeliveryType: null,
            CreatedAt: DateTimeOffset.UnixEpoch);

    private static PaymentNotificationReceived Notification(string merchantRef, bool success) =>
        new(
            PspReference: "psp-1",
            MerchantReference: merchantRef,
            EventCode: "AUTHORISATION",
            Success: success,
            Amount: 100m,
            Currency: "EUR");

    [Fact]
    public async Task Successful_payment_transitions_order_to_paid_and_upserts_projection()
    {
        var commerce = new FakeCommerceClient();
        commerce.Seed(Order("order-1", OrderStatus.Pending, PaymentStatus.Pending));
        var store = new FakeOrderProjectionStore();
        var sut = new PaymentProjector(commerce, store, NullLogger<PaymentProjector>.Instance);

        await sut.ProjectAsync(Notification("order-1", success: true), CancellationToken.None);

        commerce.Transitions.ShouldBe([("order-1", OrderStatus.Paid)]);
        store.UpsertCalls.ShouldBe(1);
        store.Upserted["order-1"].Status.ShouldBe(OrderStatus.Paid);
        store.Upserted["order-1"].PaymentStatus.ShouldBe(PaymentStatus.Captured);
    }

    [Fact]
    public async Task Failed_payment_does_not_transition_or_project()
    {
        var commerce = new FakeCommerceClient();
        commerce.Seed(Order("order-1", OrderStatus.Pending, PaymentStatus.Pending));
        var store = new FakeOrderProjectionStore();
        var sut = new PaymentProjector(commerce, store, NullLogger<PaymentProjector>.Instance);

        await sut.ProjectAsync(Notification("order-1", success: false), CancellationToken.None);

        commerce.GetOrderCalls.ShouldBe(0);
        commerce.Transitions.ShouldBeEmpty();
        store.UpsertCalls.ShouldBe(0);
    }

    [Fact]
    public async Task Redelivery_of_already_paid_order_skips_transition_but_refreshes_projection()
    {
        var commerce = new FakeCommerceClient();
        commerce.Seed(Order("order-1", OrderStatus.Paid, PaymentStatus.Captured));
        var store = new FakeOrderProjectionStore();
        var sut = new PaymentProjector(commerce, store, NullLogger<PaymentProjector>.Instance);

        await sut.ProjectAsync(Notification("order-1", success: true), CancellationToken.None);

        // Idempotent: order already Paid → no re-transition, but the read-model is still converged.
        commerce.Transitions.ShouldBeEmpty();
        store.UpsertCalls.ShouldBe(1);
        store.Upserted["order-1"].Status.ShouldBe(OrderStatus.Paid);
    }

    [Theory]
    [InlineData(OrderStatus.Fulfilling)]
    [InlineData(OrderStatus.Shipped)]
    [InlineData(OrderStatus.Delivered)]
    public async Task Order_already_past_paid_skips_transition(OrderStatus advanced)
    {
        var commerce = new FakeCommerceClient();
        commerce.Seed(Order("order-1", advanced, PaymentStatus.Captured));
        var store = new FakeOrderProjectionStore();
        var sut = new PaymentProjector(commerce, store, NullLogger<PaymentProjector>.Instance);

        await sut.ProjectAsync(Notification("order-1", success: true), CancellationToken.None);

        commerce.Transitions.ShouldBeEmpty();
        store.Upserted["order-1"].Status.ShouldBe(advanced);
    }

    [Fact]
    public async Task Unknown_order_throws_so_the_message_is_retried_then_dead_lettered()
    {
        var commerce = new FakeCommerceClient(); // nothing seeded
        var store = new FakeOrderProjectionStore();
        var sut = new PaymentProjector(commerce, store, NullLogger<PaymentProjector>.Instance);

        await Should.ThrowAsync<InvalidOperationException>(
            sut.ProjectAsync(Notification("missing-order", success: true), CancellationToken.None));

        store.UpsertCalls.ShouldBe(0);
    }

    [Fact]
    public async Task Null_notification_throws_argument_null()
    {
        var sut = new PaymentProjector(
            new FakeCommerceClient(), new FakeOrderProjectionStore(), NullLogger<PaymentProjector>.Instance);

        await Should.ThrowAsync<ArgumentNullException>(
            sut.ProjectAsync(null!, CancellationToken.None));
    }
}
