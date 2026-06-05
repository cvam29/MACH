using Mach.Application.Dtos;
using Mach.Domain.ValueObjects;

namespace Mach.Application.Ports;

/// <summary>
/// CQRS read-model store for <c>/orders/me</c>. Rebuildable from events.
/// Implemented by <c>Mach.Persistence</c>.
/// </summary>
public interface IOrderProjectionStore
{
    Task UpsertAsync(OrderDto order, CancellationToken ct);

    Task<IReadOnlyList<OrderDto>> GetByCustomerAsync(CustomerId customerId, CancellationToken ct);

    Task<OrderDto?> GetByIdAsync(OrderId orderId, CancellationToken ct);
}
