using System.Globalization;
using Mach.Application.Dtos;
using Mach.Application.Ports;
using Mach.Application.Services;
using Mach.Contracts;
using Mach.Domain;
using Mach.Domain.ValueObjects;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Mach.Notifications.Functions;

/// <summary>
/// Fans an <see cref="OrderPlaced"/> event out to the four notification audiences
/// (customer / store / supplier(s) / reception). For each audience it resolves the
/// recipient, pulls the CMS email template, renders <c>{{order.*}}</c> / <c>{{delivery.*}}</c>
/// tokens against the order, and sends via <see cref="IEmailSender"/>.
///
/// Each (orderId, audience) send is guarded by <see cref="IIdempotencyStore"/> so a
/// Service Bus re-delivery (at-least-once) does not resend mail. The whole class is an
/// injectable, fake-friendly seam — the Service Bus trigger is a thin adapter over it.
/// </summary>
public sealed class OrderNotifier(
    ICommerceClient commerce,
    IFulfillmentDirectory fulfillment,
    INotificationFanout fanout,
    ICmsClient cms,
    IEmailSender email,
    IIdempotencyStore idempotency,
    IOptions<NotificationOptions> options,
    ILogger<OrderNotifier> logger)
{
    private readonly NotificationOptions _options = options.Value;

    /// <summary>
    /// Resolves recipients, renders templates and sends one email per resolved audience.
    /// Idempotent per (orderId, audience): re-delivery of the same event sends nothing extra.
    /// </summary>
    public async Task NotifyAsync(OrderPlaced order, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(order);

        var context = await ResolveContextAsync(order, ct).ConfigureAwait(false);
        var targets = fanout.Resolve(ToOrderDto(order), context);

        logger.LogInformation(
            "OrderPlaced {OrderId} fans out to {Count} audience target(s).",
            order.OrderId,
            targets.Count);

        foreach (var target in targets)
        {
            await SendToTargetAsync(order, target, ct).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Builds the recipient context: customer email from the commerce engine (config fallback),
    /// store + reception from the nearest/first store, and one email per distinct supplier mapped
    /// to an order SKU. Unmapped SKUs are skipped gracefully.
    /// </summary>
    private async Task<NotificationFanoutContext> ResolveContextAsync(OrderPlaced order, CancellationToken ct)
    {
        var customerEmail = await ResolveCustomerEmailAsync(order, ct).ConfigureAwait(false);

        string? storeEmail = null;
        string? receptionEmail = null;

        // No structured destination on the event, so we take the first/nearest store. With a real
        // shipping address we would pass it to GetNearestStoreAsync for distance-based selection.
        var stores = await fulfillment.GetStoresAsync(ct).ConfigureAwait(false);
        if (stores.Count > 0)
        {
            var store = stores[0];
            storeEmail = store.Email;
            receptionEmail = store.ReceptionEmail;
        }

        var supplierEmails = new List<string>();
        var seenSuppliers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in order.Lines)
        {
            var supplier = await fulfillment
                .GetSupplierForSkuAsync(new Sku(line.Sku), ct)
                .ConfigureAwait(false);

            if (supplier is null)
            {
                logger.LogInformation(
                    "No supplier mapping for SKU {Sku} on order {OrderId}; skipping supplier notification for that line.",
                    line.Sku,
                    order.OrderId);
                continue;
            }

            if (!string.IsNullOrWhiteSpace(supplier.Email) && seenSuppliers.Add(supplier.Email))
            {
                supplierEmails.Add(supplier.Email);
            }
        }

        return new NotificationFanoutContext(customerEmail, storeEmail, receptionEmail, supplierEmails);
    }

    private async Task<string> ResolveCustomerEmailAsync(OrderPlaced order, CancellationToken ct)
    {
        var fallback = _options.Recipient ?? string.Empty;

        if (string.IsNullOrWhiteSpace(order.CustomerId))
        {
            return fallback;
        }

        var customer = await commerce
            .GetCustomerAsync(new CustomerId(order.CustomerId), ct)
            .ConfigureAwait(false);

        if (customer.IsSuccess && !string.IsNullOrWhiteSpace(customer.Value.Email))
        {
            return customer.Value.Email;
        }

        logger.LogWarning(
            "Could not resolve customer email for order {OrderId} (customer {CustomerId}); using config fallback.",
            order.OrderId,
            order.CustomerId);

        return fallback;
    }

    private async Task SendToTargetAsync(OrderPlaced order, NotificationTarget target, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(target.Recipient))
        {
            logger.LogWarning(
                "Skipping {Audience} notification for order {OrderId}: no recipient resolved.",
                target.Audience,
                order.OrderId);
            return;
        }

        // Idempotency guard: claim the (orderId, audience) key. A re-delivery finds the key already
        // InProgress/Completed and short-circuits, so the same audience is never mailed twice.
        var key = IdempotencyKey(order.OrderId, target.Audience);
        var state = await idempotency.TryBeginAsync(key, ct).ConfigureAwait(false);
        if (state != IdempotencyState.Began)
        {
            logger.LogInformation(
                "Skipping {Audience} notification for order {OrderId}: already {State} (dedup key {Key}).",
                target.Audience,
                order.OrderId,
                state,
                key);
            return;
        }

        var template = await cms.GetEmailTemplateAsync(target.Audience, ct).ConfigureAwait(false);
        if (template.IsFailure)
        {
            logger.LogError(
                "No CMS email template for {Audience} (order {OrderId}): {Error}.",
                target.Audience,
                order.OrderId,
                template.Error.Message);
            return;
        }

        var subject = RenderTokens(template.Value.Subject, order);
        var body = RenderTokens(template.Value.HtmlBody, order);

        var message = new EmailMessage(target.Recipient, subject, body, target.Audience);
        var send = await email.SendAsync(message, ct).ConfigureAwait(false);
        if (send.IsFailure)
        {
            logger.LogError(
                "Failed to send {Audience} notification for order {OrderId}: {Error}.",
                target.Audience,
                order.OrderId,
                send.Error.Message);

            // Leave the idempotency key InProgress so a redelivery can retry the send.
            return;
        }

        await idempotency.CompleteWithAsync(key, target.Audience.ToString(), ct).ConfigureAwait(false);

        logger.LogInformation(
            "Sent {Audience} notification for order {OrderId} to {Recipient}.",
            target.Audience,
            order.OrderId,
            target.Recipient);
    }

    /// <summary>The dedup key for an audience send: <c>email:{orderId}:{audience}</c>.</summary>
    internal static string IdempotencyKey(string orderId, NotificationAudience audience) =>
        $"email:{orderId}:{audience.ToString().ToLowerInvariant()}";

    /// <summary>
    /// Replaces the simple <c>{{order.*}}</c> / <c>{{delivery.*}}</c> tokens a CMS template may
    /// carry with values from the event. Unknown tokens are left untouched.
    /// </summary>
    private static string RenderTokens(string template, OrderPlaced order)
    {
        if (string.IsNullOrEmpty(template))
        {
            return template;
        }

        var total = order.TotalAmount.ToString("0.00", CultureInfo.InvariantCulture);
        var itemCount = order.Lines.Sum(l => l.Quantity).ToString(CultureInfo.InvariantCulture);

        return template
            .Replace("{{order.id}}", order.OrderId, StringComparison.OrdinalIgnoreCase)
            .Replace("{{order.number}}", order.OrderNumber, StringComparison.OrdinalIgnoreCase)
            .Replace("{{order.total}}", total, StringComparison.OrdinalIgnoreCase)
            .Replace("{{order.currency}}", order.Currency, StringComparison.OrdinalIgnoreCase)
            .Replace("{{order.itemCount}}", itemCount, StringComparison.OrdinalIgnoreCase)
            .Replace("{{delivery.itemCount}}", itemCount, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Projects the <see cref="OrderPlaced"/> event onto the minimal <see cref="OrderDto"/> the
    /// pure <see cref="INotificationFanout"/> needs (lines drive supplier resolution upstream;
    /// here they only need SKUs/quantities).
    /// </summary>
    private static OrderDto ToOrderDto(OrderPlaced order)
    {
        var currency = order.Currency;
        var lines = order.Lines
            .Select(l => new OrderLineDto(
                new Sku(l.Sku),
                l.Sku,
                l.Quantity,
                Money.Zero(currency),
                Money.Zero(currency)))
            .ToList();

        return new OrderDto(
            new OrderId(order.OrderId),
            order.OrderNumber,
            string.IsNullOrWhiteSpace(order.CustomerId) ? null : new CustomerId(order.CustomerId),
            OrderStatus.Pending,
            PaymentStatus.Pending,
            new Money(order.TotalAmount, currency),
            lines,
            ShippingAddress: null,
            DeliveryType: null,
            CreatedAt: order.OccurredAt);
    }
}
