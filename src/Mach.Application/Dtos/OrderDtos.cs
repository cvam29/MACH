using Mach.Domain;
using Mach.Domain.ValueObjects;

namespace Mach.Application.Dtos;

/// <summary>A line on an order.</summary>
public sealed record OrderLineDto(
    Sku Sku,
    string Name,
    int Quantity,
    Money UnitPrice,
    Money TotalPrice);

/// <summary>An order as held by the commerce engine.</summary>
public sealed record OrderDto(
    OrderId Id,
    string OrderNumber,
    CustomerId? CustomerId,
    OrderStatus Status,
    PaymentStatus PaymentStatus,
    Money TotalPrice,
    IReadOnlyList<OrderLineDto> Lines,
    Address? ShippingAddress,
    DeliveryType? DeliveryType,
    DateTimeOffset CreatedAt);
