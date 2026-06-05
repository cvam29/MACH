using Mach.Application.Dtos;
using Mach.Contracts;
using Mach.Domain;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;

namespace Mach.Webhooks.Functions.Tests;

public sealed class AdyenWebhookFunctionsTests
{
    private static AdyenWebhookFunctions Build(
        FakePaymentGateway gateway, FakeIdempotencyStore idempotency, FakeMessageBus bus)
        => new(gateway, idempotency, bus, NullLogger<AdyenWebhookFunctions>.Instance);

    [Fact]
    public void DedupKey_is_stable_and_namespaced()
        => AdyenWebhookFunctions.DedupKey("psp-9", "AUTHORISATION")
            .ShouldBe("adyen:psp-9:AUTHORISATION");

    [Fact]
    public async Task Invalid_hmac_is_rejected_with_401_and_nothing_is_published()
    {
        var gateway = new FakePaymentGateway(signatureValid: false);
        var idempotency = new FakeIdempotencyStore();
        var bus = new FakeMessageBus();
        var sut = Build(gateway, idempotency, bus);

        var request = Http.Request("{}", ("Adyen-Hmac-Signature", "bad"));
        var (status, _) = await Http.ExecuteAsync(await sut.Handle(request, default), request);

        status.ShouldBe(401);
        bus.Published.ShouldBeEmpty();
        idempotency.Began.ShouldBeEmpty();
    }

    [Fact]
    public async Task Signed_but_unparseable_body_acks_so_adyen_stops_retrying()
    {
        var gateway = new FakePaymentGateway(
            signatureValid: true,
            parseResult: Result.Failure<IReadOnlyList<PaymentNotificationDto>>(Error.Validation("bad json")));
        var bus = new FakeMessageBus();
        var sut = Build(gateway, new FakeIdempotencyStore(), bus);

        var request = Http.Request("not-json", ("Adyen-Hmac-Signature", "ok"));
        var (status, body) = await Http.ExecuteAsync(await sut.Handle(request, default), request);

        status.ShouldBe(200);
        body.ShouldBe(AdyenWebhookFunctions.AdyenAck);
        bus.Published.ShouldBeEmpty();
    }

    [Fact]
    public async Task Valid_notification_publishes_once_and_acks()
    {
        var notification = FakePaymentGateway.Notification(psp: "psp-7", merchant: "order-7", success: true);
        var gateway = new FakePaymentGateway(
            signatureValid: true,
            parseResult: Result.Success<IReadOnlyList<PaymentNotificationDto>>([notification]));
        var idempotency = new FakeIdempotencyStore();
        var bus = new FakeMessageBus();
        var sut = Build(gateway, idempotency, bus);

        var request = Http.Request("{}", ("Adyen-Hmac-Signature", "ok"));
        var (status, body) = await Http.ExecuteAsync(await sut.Handle(request, default), request);

        status.ShouldBe(200);
        body.ShouldBe(AdyenWebhookFunctions.AdyenAck);

        bus.Published.Count.ShouldBe(1);
        var (topic, message) = bus.Published[0];
        topic.ShouldBe(Topics.Payments);
        var evt = message.ShouldBeOfType<PaymentNotificationReceived>();
        evt.PspReference.ShouldBe("psp-7");
        evt.MerchantReference.ShouldBe("order-7");
        evt.Success.ShouldBeTrue();

        var key = AdyenWebhookFunctions.DedupKey("psp-7", "AUTHORISATION");
        idempotency.Began.ShouldBe([key]);
        idempotency.Completed.ShouldBe([key]);
    }

    [Fact]
    public async Task Duplicate_delivery_is_skipped_and_not_republished()
    {
        var notification = FakePaymentGateway.Notification(psp: "psp-7", merchant: "order-7");
        var gateway = new FakePaymentGateway(
            signatureValid: true,
            parseResult: Result.Success<IReadOnlyList<PaymentNotificationDto>>([notification]));
        var idempotency = new FakeIdempotencyStore();
        idempotency.SeedCompleted(AdyenWebhookFunctions.DedupKey("psp-7", "AUTHORISATION"));
        var bus = new FakeMessageBus();
        var sut = Build(gateway, idempotency, bus);

        var request = Http.Request("{}", ("Adyen-Hmac-Signature", "ok"));
        var (status, body) = await Http.ExecuteAsync(await sut.Handle(request, default), request);

        // Still ACK so Adyen stops retrying, but nothing new is published.
        status.ShouldBe(200);
        body.ShouldBe(AdyenWebhookFunctions.AdyenAck);
        bus.Published.ShouldBeEmpty();
        idempotency.Began.ShouldBeEmpty();
    }

    [Fact]
    public async Task Multiple_items_each_publish_independently()
    {
        IReadOnlyList<PaymentNotificationDto> items =
        [
            FakePaymentGateway.Notification(psp: "psp-a", eventCode: "AUTHORISATION"),
            FakePaymentGateway.Notification(psp: "psp-b", eventCode: "CAPTURE"),
        ];
        var gateway = new FakePaymentGateway(
            signatureValid: true, parseResult: Result.Success(items));
        var bus = new FakeMessageBus();
        var sut = Build(gateway, new FakeIdempotencyStore(), bus);

        var request = Http.Request("{}", ("Adyen-Hmac-Signature", "ok"));
        await Http.ExecuteAsync(await sut.Handle(request, default), request);

        bus.Published.Count.ShouldBe(2);
        bus.Published.ShouldAllBe(p => p.Topic == Topics.Payments);
    }
}
