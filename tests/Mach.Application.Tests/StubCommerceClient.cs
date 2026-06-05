using Mach.Application.Dtos;
using Mach.Application.Ports;
using Mach.Domain;
using Mach.Domain.ValueObjects;

namespace Mach.Application.Tests;

/// <summary>
/// Test double for <see cref="ICommerceClient"/>; every member throws unless overridden.
/// </summary>
public abstract class StubCommerceClient : ICommerceClient
{
    public virtual Task<Result<IReadOnlyList<CategoryDto>>> GetCategoriesAsync(CancellationToken ct)
        => throw new NotSupportedException();

    public virtual Task<Result<ProductDto>> GetProductBySlugAsync(string slug, CancellationToken ct)
        => throw new NotSupportedException();

    public virtual Task<Result<CartDto>> CreateCartAsync(string currency, string? anonymousId, CancellationToken ct)
        => throw new NotSupportedException();

    public virtual Task<Result<CartDto>> GetCartAsync(CartId cartId, CancellationToken ct)
        => throw new NotSupportedException();

    public virtual Task<Result<CartDto>> AddLineItemAsync(CartId cartId, long version, AddLineItemRequest request, CancellationToken ct)
        => throw new NotSupportedException();

    public virtual Task<Result<CartDto>> UpdateLineItemQuantityAsync(CartId cartId, long version, string lineItemId, int quantity, CancellationToken ct)
        => throw new NotSupportedException();

    public virtual Task<Result<CartDto>> RemoveLineItemAsync(CartId cartId, long version, string lineItemId, CancellationToken ct)
        => throw new NotSupportedException();

    public virtual Task<Result<CartDto>> SetCartAddressesAsync(CartId cartId, long version, Address? shipping, Address? billing, CancellationToken ct)
        => throw new NotSupportedException();

    public virtual Task<Result<CartDto>> SetShippingMethodAsync(CartId cartId, long version, ShippingSelection selection, CancellationToken ct)
        => throw new NotSupportedException();

    public virtual Task<Result<OrderDto>> CreateOrderFromCartAsync(CartId cartId, long version, CancellationToken ct)
        => throw new NotSupportedException();

    public virtual Task<Result<OrderDto>> GetOrderAsync(OrderId orderId, CancellationToken ct)
        => throw new NotSupportedException();

    public virtual Task<Result<OrderDto>> TransitionOrderAsync(OrderId orderId, OrderStatus status, CancellationToken ct)
        => throw new NotSupportedException();

    public virtual Task<Result<CustomerDto>> GetCustomerAsync(CustomerId customerId, CancellationToken ct)
        => throw new NotSupportedException();
}
