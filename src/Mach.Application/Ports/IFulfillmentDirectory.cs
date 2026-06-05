using Mach.Domain.ValueObjects;

namespace Mach.Application.Ports;

/// <summary>
/// Reads fulfillment reference data (stores + suppliers) from SQL. Powers distance-based
/// delivery quoting (nearest store) and notification recipient resolution
/// (store / reception / supplier). Implemented by <c>Mach.Persistence</c>.
/// </summary>
public interface IFulfillmentDirectory
{
    /// <summary>All fulfilling stores/warehouses with their locations and notification emails.</summary>
    Task<IReadOnlyList<StoreLocation>> GetStoresAsync(CancellationToken ct);

    /// <summary>The store geographically nearest to <paramref name="destination"/>, or null if none exist.</summary>
    Task<StoreLocation?> GetNearestStoreAsync(GeoPoint destination, CancellationToken ct);

    /// <summary>The supplier responsible for <paramref name="sku"/>, or null if unmapped.</summary>
    Task<SupplierContact?> GetSupplierForSkuAsync(Sku sku, CancellationToken ct);
}
