using Mach.Application.Dtos;
using Mach.Domain;

namespace Mach.Application.Services;

/// <summary>A single resolved notification target: which audience, who, and which template.</summary>
public sealed record NotificationTarget(
    NotificationAudience Audience,
    string Recipient,
    string TemplateKey);

/// <summary>
/// Resolves an order into the set of <see cref="NotificationTarget"/>s for the multi-party
/// fan-out (customer / store / supplier / reception).
/// </summary>
public interface INotificationFanout
{
    IReadOnlyList<NotificationTarget> Resolve(OrderDto order, NotificationFanoutContext context);
}

/// <summary>
/// Recipient context for fan-out, resolved from SQL (stores/suppliers) with config fallbacks.
/// </summary>
public sealed record NotificationFanoutContext(
    string CustomerEmail,
    string? StoreEmail,
    string? ReceptionEmail,
    IReadOnlyList<string> SupplierEmails);
