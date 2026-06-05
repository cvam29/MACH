using Mach.Application.Dtos;
using Mach.Domain;
using Mach.Domain.ValueObjects;

namespace Mach.Application.Ports;

/// <summary>
/// Port over the payment gateway (Adyen): create payment sessions, verify webhook HMAC,
/// parse notifications. Implemented by <c>Mach.Infrastructure.Adyen</c>.
/// </summary>
public interface IPaymentGateway
{
    Task<Result<PaymentSessionDto>> CreatePaymentSessionAsync(
        CartId cartId, Money amount, CancellationToken ct);

    /// <summary>Verify the HMAC signature on a raw webhook payload.</summary>
    bool VerifyWebhookSignature(string rawBody, string hmacSignature);

    /// <summary>Parse a verified webhook body into zero or more payment notifications.</summary>
    Result<IReadOnlyList<PaymentNotificationDto>> ParseNotification(string rawBody);
}
