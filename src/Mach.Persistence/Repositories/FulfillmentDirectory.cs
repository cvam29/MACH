using Mach.Application.Ports;
using Mach.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace Mach.Persistence.Repositories;

/// <summary>
/// Reads fulfillment reference data from <c>fulfillment.Stores</c> /
/// <c>fulfillment.Suppliers</c> / <c>fulfillment.ProductSuppliers</c>. Nearest-store
/// selection uses an inline great-circle (haversine) distance — pure math, with no
/// dependency on the Maps project.
/// </summary>
internal sealed class FulfillmentDirectory(MachDbContext db) : IFulfillmentDirectory
{
    public async Task<IReadOnlyList<StoreLocation>> GetStoresAsync(CancellationToken ct)
    {
        var rows = await db.Stores
            .AsNoTracking()
            .ToListAsync(ct);

        return rows.Select(ToStoreLocation).ToList();
    }

    public async Task<StoreLocation?> GetNearestStoreAsync(GeoPoint destination, CancellationToken ct)
    {
        var rows = await db.Stores
            .AsNoTracking()
            .ToListAsync(ct);

        if (rows.Count == 0)
        {
            return null;
        }

        var nearest = rows
            .OrderBy(s => Haversine.DistanceKm(destination, new GeoPoint(s.Lat, s.Lng)))
            .First();

        return ToStoreLocation(nearest);
    }

    public async Task<SupplierContact?> GetSupplierForSkuAsync(Sku sku, CancellationToken ct)
    {
        var skuValue = sku.Value;

        var row = await db.ProductSuppliers
            .AsNoTracking()
            .Where(ps => ps.Sku == skuValue)
            .Join(
                db.Suppliers.AsNoTracking(),
                ps => ps.SupplierId,
                s => s.Id,
                (ps, s) => new SupplierContact(s.Id, s.Name, s.Email))
            .FirstOrDefaultAsync(ct);

        return row;
    }

    private static StoreLocation ToStoreLocation(Entities.StoreEntity s) =>
        new(s.Id, s.Name, new GeoPoint(s.Lat, s.Lng), s.Email, s.ReceptionEmail);
}
