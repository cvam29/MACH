namespace Mach.Persistence.Entities;

/// <summary>
/// A fulfilling store / warehouse in <c>fulfillment.Stores</c>. Column names are
/// EXACT — a SQL seed script (<c>seed/sql/fulfillment-seed.sql</c>) depends on them.
/// </summary>
public sealed class StoreEntity
{
    public Guid Id { get; set; }

    public string Name { get; set; } = default!;

    public string Email { get; set; } = default!;

    public string ReceptionEmail { get; set; } = default!;

    public double Lat { get; set; }

    public double Lng { get; set; }
}

/// <summary>A supplier in <c>fulfillment.Suppliers</c>. Column names are EXACT (seed depends on them).</summary>
public sealed class SupplierEntity
{
    public Guid Id { get; set; }

    public string Name { get; set; } = default!;

    public string Email { get; set; } = default!;

    public List<ProductSupplierEntity> Products { get; set; } = [];
}

/// <summary>
/// Maps a product SKU to its supplier in <c>fulfillment.ProductSuppliers</c>.
/// Column names are EXACT (seed depends on them); keyed on <see cref="Sku"/>.
/// </summary>
public sealed class ProductSupplierEntity
{
    public string Sku { get; set; } = default!;

    public Guid SupplierId { get; set; }

    public SupplierEntity Supplier { get; set; } = default!;
}
