using Mach.Domain;
using Mach.Domain.ValueObjects;

namespace Mach.Application.Dtos;

/// <summary>A payment session created with the gateway (Adyen) for a given cart/amount.</summary>
public sealed record PaymentSessionDto(
    string SessionId,
    string SessionData,
    CartId CartId,
    Money Amount);

/// <summary>
/// A parsed, signature-verified gateway notification (e.g. Adyen webhook item).
/// </summary>
public sealed record PaymentNotificationDto(
    string PspReference,
    string MerchantReference,
    string EventCode,
    PaymentStatus Status,
    Money Amount,
    bool Success,
    DateTimeOffset EventDate);
