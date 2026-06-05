using Mach.Domain.ValueObjects;

namespace Mach.Application.Services;

/// <summary>
/// Computes distance-based delivery quotes for a cart destination against a set of fulfilling stores.
/// </summary>
public interface IDeliveryQuoting
{
    /// <summary>
    /// Produce a quote per <see cref="Mach.Domain.DeliveryType"/> for the given cart total and
    /// destination, using the nearest of <paramref name="stores"/> for distance.
    /// </summary>
    Task<IReadOnlyList<DeliveryQuote>> QuoteAsync(
        Money cartTotal,
        Address destination,
        IReadOnlyList<StoreLocation> stores,
        CancellationToken ct);
}
