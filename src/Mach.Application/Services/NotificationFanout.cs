using Mach.Application.Dtos;
using Mach.Domain;

namespace Mach.Application.Services;

/// <summary>
/// Pure fan-out: maps an order + recipient context to one notification target per audience.
/// Template keys follow the convention <c>order-{audience}</c>.
/// </summary>
public sealed class NotificationFanout : INotificationFanout
{
    public IReadOnlyList<NotificationTarget> Resolve(OrderDto order, NotificationFanoutContext context)
    {
        ArgumentNullException.ThrowIfNull(order);
        ArgumentNullException.ThrowIfNull(context);

        var targets = new List<NotificationTarget>(capacity: 4);

        // Customer — order confirmation.
        targets.Add(new NotificationTarget(
            NotificationAudience.Customer,
            context.CustomerEmail,
            TemplateKey(NotificationAudience.Customer)));

        // Store — new order to fulfil (assigned/nearest store).
        if (!string.IsNullOrWhiteSpace(context.StoreEmail))
        {
            targets.Add(new NotificationTarget(
                NotificationAudience.Store,
                context.StoreEmail,
                TemplateKey(NotificationAudience.Store)));
        }

        // Reception — goods-in / pickup-ready notice.
        if (!string.IsNullOrWhiteSpace(context.ReceptionEmail))
        {
            targets.Add(new NotificationTarget(
                NotificationAudience.Reception,
                context.ReceptionEmail,
                TemplateKey(NotificationAudience.Reception)));
        }

        // Supplier — replenishment/dropship request per distinct supplier.
        foreach (var supplier in context.SupplierEmails.Where(e => !string.IsNullOrWhiteSpace(e)).Distinct())
        {
            targets.Add(new NotificationTarget(
                NotificationAudience.Supplier,
                supplier,
                TemplateKey(NotificationAudience.Supplier)));
        }

        return targets;
    }

    private static string TemplateKey(NotificationAudience audience)
        => $"order-{audience.ToString().ToLowerInvariant()}";
}
