using Mach.Application.Dtos;
using Mach.Domain;
using Mach.Domain.ValueObjects;

namespace Mach.Application.Ports;

/// <summary>
/// Port over the commerce engine (commercetools): catalog, cart, order and customer reads/writes.
/// Implemented by <c>Mach.Infrastructure.Commercetools</c>.
/// </summary>
public interface ICommerceClient
{
    Task<Result<IReadOnlyList<CategoryDto>>> GetCategoriesAsync(CancellationToken ct);

    Task<Result<ProductDto>> GetProductBySlugAsync(string slug, CancellationToken ct);

    Task<Result<CartDto>> CreateCartAsync(string currency, string? anonymousId, CancellationToken ct);

    Task<Result<CartDto>> GetCartAsync(CartId cartId, CancellationToken ct);

    Task<Result<CartDto>> AddLineItemAsync(
        CartId cartId, long version, AddLineItemRequest request, CancellationToken ct);

    Task<Result<CartDto>> UpdateLineItemQuantityAsync(
        CartId cartId, long version, string lineItemId, int quantity, CancellationToken ct);

    Task<Result<CartDto>> RemoveLineItemAsync(
        CartId cartId, long version, string lineItemId, CancellationToken ct);

    Task<Result<CartDto>> SetCartAddressesAsync(
        CartId cartId, long version, Address? shipping, Address? billing, CancellationToken ct);

    Task<Result<CartDto>> SetShippingMethodAsync(
        CartId cartId, long version, ShippingSelection selection, CancellationToken ct);

    Task<Result<OrderDto>> CreateOrderFromCartAsync(CartId cartId, long version, CancellationToken ct);

    Task<Result<OrderDto>> GetOrderAsync(OrderId orderId, CancellationToken ct);

    Task<Result<OrderDto>> TransitionOrderAsync(
        OrderId orderId, OrderStatus status, CancellationToken ct);

    Task<Result<CustomerDto>> GetCustomerAsync(CustomerId customerId, CancellationToken ct);
}
