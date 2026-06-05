namespace Mach.Persistence.Entities;

/// <summary>
/// CQRS read-model row in <c>orders.OrderProjections</c>. Rebuildable from events.
/// </summary>
public sealed class OrderProjectionEntity
{
    public string OrderId { get; set; } = default!;

    public string? CustomerId { get; set; }

    public string Number { get; set; } = default!;

    public string Status { get; set; } = default!;

    public string PaymentStatus { get; set; } = default!;

    public decimal TotalGross { get; set; }

    public string Currency { get; set; } = default!;

    public DateTimeOffset PlacedUtc { get; set; }

    public DateTimeOffset UpdatedUtc { get; set; }

    /// <summary>Optimistic-concurrency token (SQL Server rowversion).</summary>
    public byte[] RowVersion { get; set; } = default!;

    public List<OrderLineProjectionEntity> Lines { get; set; } = [];
}

/// <summary>A line on an <see cref="OrderProjectionEntity"/> in <c>orders.OrderLineProjections</c>.</summary>
public sealed class OrderLineProjectionEntity
{
    public Guid Id { get; set; }

    public string OrderId { get; set; } = default!;

    public string Sku { get; set; } = default!;

    public string Name { get; set; } = default!;

    public int Quantity { get; set; }

    public decimal UnitPriceGross { get; set; }

    public string Currency { get; set; } = default!;

    public OrderProjectionEntity Order { get; set; } = default!;
}
