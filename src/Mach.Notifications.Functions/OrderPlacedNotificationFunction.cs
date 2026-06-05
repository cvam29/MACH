using System.Text.Json;
using Mach.Contracts;
using Mach.ServiceDefaults;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace Mach.Notifications.Functions;

/// <summary>
/// Service Bus trigger on the <c>notifications</c> topic (subscription <c>notifications</c>,
/// connection <c>ServiceBusConnection</c>). Deserializes an <see cref="OrderPlaced"/> event and
/// delegates to <see cref="OrderNotifier"/> for the multi-party email fan-out.
///
/// NOTE: with <c>Messaging:Provider=InMemory</c> (offline, single-process) nothing publishes to a
/// real broker, so this trigger does not fire — that is expected. The trigger targets the Azure
/// Service Bus emulator or a real Azure Service Bus namespace. In offline runs the dev-sink email
/// sender (<c>Email:Provider=DevSink</c>) writes each rendered message as an <c>.eml</c> file under
/// <c>./mail</c>.
/// </summary>
public sealed class OrderPlacedNotificationFunction(
    OrderNotifier notifier,
    ILogger<OrderPlacedNotificationFunction> logger)
{
    private const string TopicName = "notifications";
    private const string SubscriptionName = "notifications";

    [Function(nameof(OrderPlacedNotificationFunction))]
    public async Task RunAsync(
        [ServiceBusTrigger(TopicName, SubscriptionName, Connection = "ServiceBusConnection")]
        string messageBody,
        FunctionContext context)
    {
        var ct = context.CancellationToken;

        OrderPlaced? order;
        try
        {
            order = JsonSerializer.Deserialize<OrderPlaced>(messageBody, MachJsonOptions.Default);
        }
        catch (JsonException ex)
        {
            // Poison message: log and let it complete so it does not loop. Real systems would
            // dead-letter here; the at-least-once trigger handles transient failures via retry.
            logger.LogError(ex, "Failed to deserialize OrderPlaced from notifications topic; dropping message.");
            return;
        }

        if (order is null)
        {
            logger.LogWarning("Received an empty/null OrderPlaced payload on notifications topic; ignoring.");
            return;
        }

        await notifier.NotifyAsync(order, ct).ConfigureAwait(false);
    }
}
