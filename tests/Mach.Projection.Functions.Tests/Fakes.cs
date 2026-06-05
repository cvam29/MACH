using Mach.Application.Dtos;
using Mach.Application.Ports;
using Mach.Domain;
using Mach.Domain.ValueObjects;

namespace Mach.Projection.Functions.Tests;

/// <summary>
/// In-memory fake commerce client backed by a dictionary of orders keyed by id. Records the
/// transitions requested so tests can assert idempotency (no double-apply).
/// </summary>
internal sealed class FakeCommerceClient : ICommerceClient
{
    private readonly Dictionary<string, OrderDto> _orders = new(StringComparer.Ordinal);

    public List<(string OrderId, OrderStatus Status)> Transitions { get; } = [];

    public int GetOrderCalls { get; private set; }

    public void Seed(OrderDto order) => _orders[order.Id.Value] = order;

    public Task<Result<OrderDto>> GetOrderAsync(OrderId orderId, CancellationToken ct)
    {
        GetOrderCalls++;
        return Task.FromResult(_orders.TryGetValue(orderId.Value, out var order)
            ? Result.Success(order)
            : Result.Failure<OrderDto>(Error.NotFound($"order '{orderId.Value}' not found")));
    }

    public Task<Result<OrderDto>> TransitionOrderAsync(OrderId orderId, OrderStatus status, CancellationToken ct)
    {
        Transitions.Add((orderId.Value, status));

        if (!_orders.TryGetValue(orderId.Value, out var order))
        {
            return Task.FromResult(Result.Failure<OrderDto>(Error.NotFound($"order '{orderId.Value}' not found")));
        }

        var paymentStatus = status == OrderStatus.Paid ? PaymentStatus.Captured : order.PaymentStatus;
        var updated = order with { Status = status, PaymentStatus = paymentStatus };
        _orders[orderId.Value] = updated;
        return Task.FromResult(Result.Success(updated));
    }

    // --- Unused members for these tests ------------------------------------------------------

    public Task<Result<IReadOnlyList<CategoryDto>>> GetCategoriesAsync(CancellationToken ct)
        => throw new NotSupportedException();

    public Task<Result<ProductDto>> GetProductBySlugAsync(string slug, CancellationToken ct)
        => throw new NotSupportedException();

    public Task<Result<CartDto>> CreateCartAsync(string currency, string? anonymousId, CancellationToken ct)
        => throw new NotSupportedException();

    public Task<Result<CartDto>> GetCartAsync(CartId cartId, CancellationToken ct)
        => throw new NotSupportedException();

    public Task<Result<CartDto>> AddLineItemAsync(CartId cartId, long version, AddLineItemRequest request, CancellationToken ct)
        => throw new NotSupportedException();

    public Task<Result<CartDto>> UpdateLineItemQuantityAsync(CartId cartId, long version, string lineItemId, int quantity, CancellationToken ct)
        => throw new NotSupportedException();

    public Task<Result<CartDto>> RemoveLineItemAsync(CartId cartId, long version, string lineItemId, CancellationToken ct)
        => throw new NotSupportedException();

    public Task<Result<CartDto>> SetCartAddressesAsync(CartId cartId, long version, Address? shipping, Address? billing, CancellationToken ct)
        => throw new NotSupportedException();

    public Task<Result<CartDto>> SetShippingMethodAsync(CartId cartId, long version, ShippingSelection selection, CancellationToken ct)
        => throw new NotSupportedException();

    public Task<Result<OrderDto>> CreateOrderFromCartAsync(CartId cartId, long version, CancellationToken ct)
        => throw new NotSupportedException();

    public Task<Result<CustomerDto>> GetCustomerAsync(CustomerId customerId, CancellationToken ct)
        => throw new NotSupportedException();
}

/// <summary>In-memory fake read-model store recording upserts keyed by order id.</summary>
internal sealed class FakeOrderProjectionStore : IOrderProjectionStore
{
    public Dictionary<string, OrderDto> Upserted { get; } = new(StringComparer.Ordinal);

    public int UpsertCalls { get; private set; }

    public Task UpsertAsync(OrderDto order, CancellationToken ct)
    {
        UpsertCalls++;
        Upserted[order.Id.Value] = order;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<OrderDto>> GetByCustomerAsync(CustomerId customerId, CancellationToken ct)
        => throw new NotSupportedException();

    public Task<OrderDto?> GetByIdAsync(OrderId orderId, CancellationToken ct)
    {
        Upserted.TryGetValue(orderId.Value, out var order);
        return Task.FromResult(order);
    }
}
