using System.Text.Json;
using Mach.Contracts;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace Mach.Projection.Functions;

/// <summary>
/// Service Bus-triggered entry point that consumes <see cref="PaymentNotificationReceived"/> events
/// from the <c>payments</c> topic and drives the order read-model projection.
/// </summary>
/// <remarks>
/// <para>
/// The trigger binds to the <c>payments</c> topic / <c>projection</c> subscription using the
/// <c>ServiceBusConnection</c> connection setting. The extension auto-completes the message on
/// success; when <see cref="PaymentProjector.ProjectAsync"/> throws, the message is abandoned and
/// redelivered, and after the subscription's max delivery count it is dead-lettered by the broker
/// (the extension's default poison-message behavior — no manual settling required here).
/// </para>
/// <para>
/// <b>Offline note.</b> With <c>Messaging:Provider=InMemory</c> (the single-process, no-dependency
/// mode) there is no Service Bus, so this trigger does not fire — that is expected. The
/// Service Bus-triggered path targets the Service Bus emulator or a real Azure Service Bus namespace.
/// </para>
/// </remarks>
public sealed class PaymentProjectionFunction(
    PaymentProjector projector,
    ILogger<PaymentProjectionFunction> logger)
{
    // Match the Web (camelCase) JSON convention used by the publishing ServiceBusMessageBus.
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly PaymentProjector _projector = projector;
    private readonly ILogger<PaymentProjectionFunction> _logger = logger;

    [Function(nameof(PaymentProjectionFunction))]
    public async Task RunAsync(
        [ServiceBusTrigger(
            topicName: Topics.Payments,
            subscriptionName: "projection",
            Connection = "ServiceBusConnection")]
        string messageBody,
        CancellationToken ct)
    {
        PaymentNotificationReceived? notification;
        try
        {
            notification = JsonSerializer.Deserialize<PaymentNotificationReceived>(messageBody, JsonOptions);
        }
        catch (JsonException ex)
        {
            // Malformed payload: not retryable. Throwing lets it dead-letter after max delivery
            // rather than looping forever on a message that can never parse.
            _logger.LogError(ex, "Could not deserialize PaymentNotificationReceived; message will be dead-lettered.");
            throw;
        }

        if (notification is null)
        {
            _logger.LogError("PaymentNotificationReceived payload deserialized to null; dead-lettering.");
            throw new InvalidOperationException("PaymentNotificationReceived payload was null.");
        }

        // Delegate to the injectable projector. Any exception bubbles up so the SB extension
        // abandons the message (retry, then dead-letter) — guaranteeing at-least-once processing.
        await _projector.ProjectAsync(notification, ct).ConfigureAwait(false);
    }
}
