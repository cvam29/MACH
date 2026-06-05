using Mach.Application.Dtos;
using Mach.Application.Services;
using Mach.Domain;
using Mach.Domain.ValueObjects;
using Shouldly;

namespace Mach.Application.Tests;

public class NotificationFanoutTests
{
    private static readonly OrderDto Order = new(
        new OrderId("o1"),
        "1001",
        new CustomerId("c1"),
        OrderStatus.Paid,
        PaymentStatus.Authorized,
        new Money(99m, "EUR"),
        [new OrderLineDto(new Sku("SKU1"), "Item", 1, new Money(99m, "EUR"), new Money(99m, "EUR"))],
        ShippingAddress: null,
        DeliveryType.Standard,
        DateTimeOffset.UtcNow);

    [Fact]
    public void Resolve_AllFourAudiences_WhenAllRecipientsPresent()
    {
        var context = new NotificationFanoutContext(
            "cust@x.test", "store@x.test", "reception@x.test", ["sup1@x.test", "sup2@x.test"]);

        var targets = new NotificationFanout().Resolve(Order, context);

        targets.Select(t => t.Audience).ShouldContain(NotificationAudience.Customer);
        targets.Select(t => t.Audience).ShouldContain(NotificationAudience.Store);
        targets.Select(t => t.Audience).ShouldContain(NotificationAudience.Reception);
        targets.Count(t => t.Audience == NotificationAudience.Supplier).ShouldBe(2);
    }

    [Fact]
    public void Resolve_CustomerAlwaysIncluded_EvenWithNoOtherRecipients()
    {
        var context = new NotificationFanoutContext("cust@x.test", null, null, []);

        var targets = new NotificationFanout().Resolve(Order, context);

        targets.ShouldHaveSingleItem();
        targets[0].Audience.ShouldBe(NotificationAudience.Customer);
        targets[0].TemplateKey.ShouldBe("order-customer");
    }

    [Fact]
    public void Resolve_DeduplicatesSupplierEmails()
    {
        var context = new NotificationFanoutContext(
            "cust@x.test", null, null, ["dup@x.test", "dup@x.test"]);

        var targets = new NotificationFanout().Resolve(Order, context);

        targets.Count(t => t.Audience == NotificationAudience.Supplier).ShouldBe(1);
    }
}
