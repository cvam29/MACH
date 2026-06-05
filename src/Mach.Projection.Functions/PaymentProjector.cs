using Mach.Application.Dtos;
using Mach.Application.Ports;
using Mach.Contracts;
using Mach.Domain;
using Mach.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace Mach.Projection.Functions;

/// <summary>
/// Pure, injectable handler for <see cref="PaymentNotificationReceived"/> events. Factored out of
/// the Service Bus function so the projection logic can be unit-tested without a Service Bus host.
/// </summary>
/// <remarks>
/// Flow for a <em>successful</em> payment:
/// <list type="number">
/// <item>Transition the order in commercetools to the paid state (<see cref="OrderStatus.Paid"/>).</item>
/// <item>Read the resulting order back from commercetools.</item>
/// <item>Upsert the order read-model via <see cref="IOrderProjectionStore"/>.</item>
/// </list>
/// <para>
/// <b>Idempotency.</b> Service Bus guarantees at-least-once delivery, so the same notification may
/// be processed more than once. The handler is safe under re-delivery:
/// </para>
/// <list type="bullet">
/// <item>The read-model write is an <em>upsert</em> keyed by order id, so replaying produces the same
/// row rather than a duplicate.</item>
/// <item>Before transitioning, the current order state is inspected; if the order has already been
/// advanced to (or past) the paid state, the transition is skipped — no double-apply, and the
/// projection is simply refreshed to converge on the latest commercetools state.</item>
/// </list>
/// <para>
/// A <em>failed</em> payment does not create or advance an order projection: a refused payment leaves
/// the order in its pre-payment state. The handler short-circuits and the message is completed
/// (nothing actionable to project).
/// </para>
/// </remarks>
public sealed class PaymentProjector(
    ICommerceClient commerce,
    IOrderProjectionStore projectionStore,
    ILogger<PaymentProjector> logger)
{
    private readonly ICommerceClient _commerce = commerce;
    private readonly IOrderProjectionStore _projectionStore = projectionStore;
    private readonly ILogger<PaymentProjector> _logger = logger;

    /// <summary>
    /// Order states that already reflect a captured/applied payment. If the order is in any of
    /// these we must not re-issue the paid transition (idempotency guard against re-delivery).
    /// </summary>
    private static readonly HashSet<OrderStatus> PaidOrLater =
    [
        OrderStatus.Paid,
        OrderStatus.Fulfilling,
        OrderStatus.Shipped,
        OrderStatus.Delivered,
    ];

    /// <summary>
    /// Applies a payment notification to the commerce engine and the order read-model.
    /// Throws on a non-recoverable commerce error so the Service Bus extension can abandon the
    /// message; after max delivery attempts the broker dead-letters it.
    /// </summary>
    public async Task ProjectAsync(PaymentNotificationReceived notification, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(notification);

        // The MerchantReference carried on the gateway notification is the commercetools order
        // identifier used when the payment session was created. We use it as the order id.
        var orderId = new OrderId(notification.MerchantReference);

        if (!notification.Success)
        {
            // Refused/failed payment: nothing to project. The order stays in its pre-payment state.
            _logger.LogInformation(
                "Payment {PspReference} for order {OrderId} was not successful ({EventCode}); no projection applied.",
                notification.PspReference, orderId.Value, notification.EventCode);
            return;
        }

        // Read current order state so we can (a) make the transition idempotent and (b) know what to
        // project. A not-found here is treated as transient/poison: throw so the message is retried
        // and eventually dead-lettered rather than silently dropped.
        var currentResult = await _commerce.GetOrderAsync(orderId, ct).ConfigureAwait(false);
        if (currentResult.IsFailure)
        {
            throw new InvalidOperationException(
                $"Failed to load order '{orderId.Value}' for payment {notification.PspReference}: " +
                $"{currentResult.Error.Code} {currentResult.Error.Message}");
        }

        var order = currentResult.Value;

        if (PaidOrLater.Contains(order.Status))
        {
            // Already applied (duplicate delivery, or a later lifecycle event already advanced the
            // order). Skip the transition; just converge the read-model on the current state.
            _logger.LogInformation(
                "Order {OrderId} already in state {Status}; skipping paid transition (idempotent).",
                orderId.Value, order.Status);
        }
        else
        {
            var transition = await _commerce
                .TransitionOrderAsync(orderId, OrderStatus.Paid, ct)
                .ConfigureAwait(false);

            if (transition.IsFailure)
            {
                throw new InvalidOperationException(
                    $"Failed to transition order '{orderId.Value}' to Paid for payment " +
                    $"{notification.PspReference}: {transition.Error.Code} {transition.Error.Message}");
            }

            // Use the post-transition order as the source of truth for the projection.
            order = transition.Value;

            _logger.LogInformation(
                "Order {OrderId} transitioned to Paid for payment {PspReference}.",
                orderId.Value, notification.PspReference);
        }

        // Upsert is keyed by order id, so re-delivery refreshes the same row rather than duplicating.
        await _projectionStore.UpsertAsync(order, ct).ConfigureAwait(false);

        _logger.LogInformation(
            "Order projection upserted for {OrderId} (status {Status}, payment {PaymentStatus}).",
            orderId.Value, order.Status, order.PaymentStatus);
    }
}
