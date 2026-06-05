using Mach.Domain;
using Mach.Domain.ValueObjects;

namespace Mach.Application.Dtos;

/// <summary>A line item inside a cart.</summary>
public sealed record LineItemDto(
    string Id,
    Sku Sku,
    string Name,
    int Quantity,
    Money UnitPrice,
    Money TotalPrice);

/// <summary>
/// A commercetools cart. <see cref="Version"/> carries the optimistic-concurrency token
/// that mutations must echo back.
/// </summary>
public sealed record CartDto(
    CartId Id,
    long Version,
    string Currency,
    IReadOnlyList<LineItemDto> LineItems,
    Money TotalPrice,
    Address? ShippingAddress,
    Address? BillingAddress,
    string? ShippingMethodId,
    CustomerId? CustomerId,
    string? AnonymousId);

/// <summary>Request to add a line item to a cart.</summary>
public sealed record AddLineItemRequest(Sku Sku, int Quantity);

/// <summary>
/// An externally-priced shipping selection written back to the commercetools cart
/// (the chosen delivery type plus the distance-computed price).
/// </summary>
public sealed record ShippingSelection(
    string ShippingMethodId,
    DeliveryType DeliveryType,
    Money ExternalPrice);
