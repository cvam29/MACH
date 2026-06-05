using Mach.Application.Dtos;
using Mach.Application.Ports;
using Mach.Contracts;

using Microsoft.AspNetCore.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace Mach.Webhooks.Functions;

/// <summary>
/// Receives inbound Adyen payment notifications at <c>/api/hooks/adyen</c>.
/// Flow: read raw body → verify HMAC → parse → per item dedup via the idempotency inbox →
/// publish <see cref="PaymentNotificationReceived"/> to <see cref="Topics.Payments"/> once →
/// ACK fast with Adyen's required <c>[accepted]</c> response.
/// </summary>
public sealed class AdyenWebhookFunctions(
    IPaymentGateway gateway,
    IIdempotencyStore idempotency,
    IMessageBus bus,
    ILogger<AdyenWebhookFunctions> logger)
{
    /// <summary>Adyen's required acknowledgement body. Anything else triggers redelivery.</summary>
    public const string AdyenAck = "[accepted]";

    /// <summary>Header Adyen can carry the notification HMAC on (also embedded in additionalData).</summary>
    private const string HmacHeader = "Adyen-Hmac-Signature";

    [Function("AdyenWebhook")]
    public async Task<IResult> Handle(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "hooks/adyen")] HttpRequest request,
        CancellationToken ct)
    {
        var rawBody = await WebhookRequest.ReadRawBodyAsync(request, ct).ConfigureAwait(false);

        var headerSignature = request.Headers.TryGetValue(HmacHeader, out var values)
            ? values.ToString()
            : string.Empty;

        // Reject anything whose HMAC does not validate. Adyen treats a 401 as "do not mark delivered".
        if (!gateway.VerifyWebhookSignature(rawBody, headerSignature))
        {
            logger.LogWarning("Rejected Adyen webhook with invalid HMAC signature.");
            return Results.Unauthorized();
        }

        var parsed = gateway.ParseNotification(rawBody);
        if (parsed.IsFailure)
        {
            // Malformed but signed: log and still ACK so Adyen stops retrying a body we cannot use.
            logger.LogWarning(
                "Adyen webhook passed HMAC but could not be parsed: {Error}", parsed.Error.Message);
            return AcceptedResult();
        }

        foreach (var item in parsed.Value)
        {
            await ProcessAsync(item, ct).ConfigureAwait(false);
        }

        // ACK fast regardless of per-item outcome; processing happens off the publish path.
        return AcceptedResult();
    }

    private async Task ProcessAsync(PaymentNotificationDto item, CancellationToken ct)
    {
        var key = DedupKey(item.PspReference, item.EventCode);

        var state = await idempotency.TryBeginAsync(key, ct).ConfigureAwait(false);
        if (state != IdempotencyState.Began)
        {
            // Replay: a previous delivery already published (or is publishing) this notification.
            logger.LogInformation(
                "Skipping duplicate Adyen notification {Key} (state {State}).", key, state);
            return;
        }

        var evt = new PaymentNotificationReceived(
            PspReference: item.PspReference,
            MerchantReference: item.MerchantReference,
            EventCode: item.EventCode,
            Success: item.Success,
            Amount: item.Amount.Amount,
            Currency: item.Amount.Currency);

        await bus.PublishAsync(Topics.Payments, evt, ct).ConfigureAwait(false);
        await idempotency.CompleteWithAsync(key, AdyenAck, ct).ConfigureAwait(false);

        logger.LogInformation(
            "Published payment notification {Key} ({EventCode}/{Success}).",
            key, item.EventCode, item.Success);
    }

    /// <summary>The inbox/dedup key for an Adyen notification item.</summary>
    public static string DedupKey(string pspReference, string eventCode)
        => $"adyen:{pspReference}:{eventCode}";

    private static IResult AcceptedResult()
        => Results.Text(AdyenAck, contentType: "text/plain", statusCode: StatusCodes.Status200OK);
}
