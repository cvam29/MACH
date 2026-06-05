using Mach.Application.Dtos;
using Mach.Domain;
using Mach.Domain.ValueObjects;

using AdyenCheckoutAmount = Adyen.Checkout.Models.Amount;
using CreateCheckoutSessionRequest = Adyen.Checkout.Models.CreateCheckoutSessionRequest;
using CreateCheckoutSessionResponse = Adyen.Checkout.Models.CreateCheckoutSessionResponse;
using NotificationRequestItem = Adyen.Webhooks.Models.NotificationRequestItem;

namespace Mach.Infrastructure.Adyen;

/// <summary>
/// Pure mapping helpers between our domain/DTO types and the Adyen Checkout/Webhook models used
/// by the Adyen .NET SDK. Isolated from any I/O so the request-building and response/notification
/// mapping can be unit-tested directly.
/// </summary>
internal static class AdyenMapping
{
    /// <summary>
    /// Converts a major-unit <see cref="Money"/> into Adyen minor units (e.g. 12.34 EUR → 1234).
    /// Assumes the two-decimal currencies used by the demo.
    /// </summary>
    public static long ToMinorUnits(Money money)
        => (long)decimal.Round(money.Amount * 100m, 0, MidpointRounding.AwayFromZero);

    /// <summary>Converts Adyen minor units back into a major-unit <see cref="Money"/>.</summary>
    public static Money FromMinorUnits(long value, string currency)
        => new(value / 100m, currency);

    /// <summary>Builds a well-formed Adyen <c>/sessions</c> request for the given cart and amount.</summary>
    public static CreateCheckoutSessionRequest BuildSessionRequest(
        AdyenOptions options, CartId cartId, Money amount)
    {
        var adyenAmount = new AdyenCheckoutAmount(amount.Currency, ToMinorUnits(amount));

        return new CreateCheckoutSessionRequest(
            amount: adyenAmount,
            merchantAccount: options.MerchantAccount,
            reference: cartId.Value,
            returnUrl: options.ReturnUrl);
    }

    /// <summary>Maps an Adyen session response onto our <see cref="PaymentSessionDto"/>.</summary>
    public static Result<PaymentSessionDto> MapSessionResponse(
        CreateCheckoutSessionResponse? response, CartId cartId)
    {
        if (response is null)
        {
            return Error.Unexpected("Adyen returned an empty session response.");
        }

        if (string.IsNullOrEmpty(response.Id) || string.IsNullOrEmpty(response.SessionData))
        {
            return Error.Unexpected("Adyen session response is missing the session id or data.");
        }

        var amount = response.Amount is { } a && a.Currency is not null
            ? FromMinorUnits(a.Value, a.Currency)
            : Money.Zero("EUR");

        return new PaymentSessionDto(response.Id, response.SessionData, cartId, amount);
    }

    /// <summary>
    /// Maps an Adyen <see cref="NotificationRequestItem"/> onto our normalized
    /// <see cref="PaymentNotificationDto"/>.
    /// </summary>
    public static PaymentNotificationDto MapNotificationItem(NotificationRequestItem item)
    {
        var currency = item.Amount?.Currency ?? "EUR";
        var minor = item.Amount?.Value ?? 0L;
        var amount = FromMinorUnits(minor, currency);

        return new PaymentNotificationDto(
            PspReference: item.PspReference ?? string.Empty,
            MerchantReference: item.MerchantReference ?? string.Empty,
            EventCode: item.EventCode ?? string.Empty,
            Status: MapStatus(item.EventCode, item.Success),
            Amount: amount,
            Success: item.Success,
            EventDate: ParseEventDate(item.EventDate));
    }

    /// <summary>Maps an Adyen event code + success flag to our <see cref="PaymentStatus"/>.</summary>
    public static PaymentStatus MapStatus(string? eventCode, bool success)
        => (eventCode?.ToUpperInvariant()) switch
        {
            "AUTHORISATION" => success ? PaymentStatus.Authorized : PaymentStatus.Refused,
            "CAPTURE" => success ? PaymentStatus.Captured : PaymentStatus.Refused,
            "REFUND" => success ? PaymentStatus.Refunded : PaymentStatus.Refused,
            "CANCELLATION" or "CANCEL_OR_REFUND" => PaymentStatus.Refused,
            _ => success ? PaymentStatus.Pending : PaymentStatus.Refused,
        };

    private static DateTimeOffset ParseEventDate(string? eventDate)
        => DateTimeOffset.TryParse(eventDate, out var parsed) ? parsed : DateTimeOffset.MinValue;
}
