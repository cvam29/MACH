using Mach.Application.Dtos;
using Mach.Application.Ports;
using Mach.Domain;
using Mach.Domain.ValueObjects;
using Mach.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace Mach.Persistence.Repositories;

/// <summary>
/// CQRS read-model store backed by <c>orders.OrderProjections</c> /
/// <c>orders.OrderLineProjections</c>. Maps to and from <see cref="OrderDto"/>.
/// </summary>
internal sealed class OrderProjectionStore(MachDbContext db, TimeProvider time) : IOrderProjectionStore
{
    public async Task UpsertAsync(OrderDto order, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(order);

        var orderId = order.Id.Value;
        var now = time.GetUtcNow();

        var existing = await db.OrderProjections
            .Include(x => x.Lines)
            .FirstOrDefaultAsync(x => x.OrderId == orderId, ct);

        if (existing is null)
        {
            var entity = new OrderProjectionEntity
            {
                OrderId = orderId,
                PlacedUtc = order.CreatedAt,
            };
            Apply(entity, order, now);
            db.OrderProjections.Add(entity);
        }
        else
        {
            Apply(existing, order, now);

            // Replace lines wholesale — projection is rebuildable, so simplest is correct.
            db.OrderLineProjections.RemoveRange(existing.Lines);
            existing.Lines = BuildLines(order, orderId);
        }

        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<OrderDto>> GetByCustomerAsync(CustomerId customerId, CancellationToken ct)
    {
        var id = customerId.Value;

        var rows = await db.OrderProjections
            .AsNoTracking()
            .Include(x => x.Lines)
            .Where(x => x.CustomerId == id)
            .OrderByDescending(x => x.PlacedUtc)
            .ToListAsync(ct);

        return rows.Select(ToDto).ToList();
    }

    public async Task<OrderDto?> GetByIdAsync(OrderId orderId, CancellationToken ct)
    {
        var id = orderId.Value;

        var row = await db.OrderProjections
            .AsNoTracking()
            .Include(x => x.Lines)
            .FirstOrDefaultAsync(x => x.OrderId == id, ct);

        return row is null ? null : ToDto(row);
    }

    private static void Apply(OrderProjectionEntity entity, OrderDto order, DateTimeOffset now)
    {
        entity.CustomerId = order.CustomerId?.Value;
        entity.Number = order.OrderNumber;
        entity.Status = order.Status.ToString();
        entity.PaymentStatus = order.PaymentStatus.ToString();
        entity.TotalGross = order.TotalPrice.Amount;
        entity.Currency = order.TotalPrice.Currency;
        entity.UpdatedUtc = now;
    }

    private static List<OrderLineProjectionEntity> BuildLines(OrderDto order, string orderId) =>
        order.Lines.Select(l => new OrderLineProjectionEntity
        {
            Id = SequentialGuid.NewGuid(),
            OrderId = orderId,
            Sku = l.Sku.Value,
            Name = l.Name,
            Quantity = l.Quantity,
            UnitPriceGross = l.UnitPrice.Amount,
            Currency = l.UnitPrice.Currency,
        }).ToList();

    private static OrderDto ToDto(OrderProjectionEntity row)
    {
        var status = Enum.TryParse<OrderStatus>(row.Status, out var s) ? s : OrderStatus.Pending;
        var paymentStatus = Enum.TryParse<PaymentStatus>(row.PaymentStatus, out var p)
            ? p
            : PaymentStatus.Pending;

        var lines = row.Lines
            .Select(l => new OrderLineDto(
                new Sku(l.Sku),
                l.Name,
                l.Quantity,
                new Money(l.UnitPriceGross, l.Currency),
                new Money(l.UnitPriceGross * l.Quantity, l.Currency)))
            .ToList();

        return new OrderDto(
            new OrderId(row.OrderId),
            row.Number,
            row.CustomerId is null ? null : new CustomerId(row.CustomerId),
            status,
            paymentStatus,
            new Money(row.TotalGross, row.Currency),
            lines,
            ShippingAddress: null,
            DeliveryType: null,
            CreatedAt: row.PlacedUtc);
    }
}
