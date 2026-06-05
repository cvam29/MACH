using Mach.Application.Dtos;
using Mach.Domain;
using Mach.Domain.ValueObjects;

namespace Mach.Bff.Functions;

// ---- Cart ------------------------------------------------------------------

/// <summary>Body for <c>POST /carts</c>.</summary>
public sealed record CreateCartBody(string? Currency);

/// <summary>
/// Body for <c>PATCH /carts/{id}/line-items</c>. <see cref="Op"/> is <c>add</c> | <c>update</c> |
/// <c>remove</c>. <see cref="Version"/> carries the optimistic-concurrency token.
/// </summary>
public sealed record LineItemMutationBody(
    string? Op,
    long Version,
    string? Sku,
    string? LineItemId,
    int Quantity);

/// <summary>An address as supplied by the storefront.</summary>
public sealed record AddressBody(
    string? Street,
    string? City,
    string? PostalCode,
    string? Country,
    string? State,
    string? FirstName,
    string? LastName)
{
    public Address ToAddress() => new(
        Street ?? string.Empty,
        City ?? string.Empty,
        PostalCode ?? string.Empty,
        Country ?? string.Empty,
        State,
        FirstName,
        LastName);
}

/// <summary>Body for <c>POST /carts/{id}/shipping</c> and <c>/billing</c>.</summary>
public sealed record SetAddressBody(long Version, AddressBody? Address);

/// <summary>Body for <c>POST /carts/{id}/delivery-options</c>.</summary>
public sealed record DeliveryOptionsBody(AddressBody? ShippingAddress);

/// <summary>Body for <c>PUT /carts/{id}/delivery</c> (the chosen delivery type + cart version).</summary>
public sealed record SetDeliveryBody(long Version, DeliveryType DeliveryType, string? ShippingMethodId);

// ---- Checkout / orders -----------------------------------------------------

/// <summary>Body for <c>POST /checkout/{cartId}/order</c> (the cart version to convert).</summary>
public sealed record PlaceOrderBody(long Version);

// ---- Search / catalog ------------------------------------------------------

/// <summary>A trimmed product summary for the SSR catalog grid.</summary>
public sealed record ProductSummaryDto(
    string Id,
    string Slug,
    string Name,
    Money? Price,
    string? ImageUrl,
    bool InStock)
{
    public static ProductSummaryDto From(ProductDto product)
    {
        var variant = product.Variants.Count > 0 ? product.Variants[0] : null;
        return new ProductSummaryDto(
            product.Id.Value,
            product.Slug,
            product.Name,
            variant?.Price,
            variant?.ImageUrls.Count > 0 ? variant.ImageUrls[0] : null,
            variant?.InStock ?? false);
    }
}

/// <summary>A page of product summaries for <c>GET /search</c>.</summary>
public sealed record ProductPageDto(
    IReadOnlyList<ProductSummaryDto> Items,
    int Page,
    int PageSize,
    int Total);

// ---- Stores ----------------------------------------------------------------

/// <summary>A store with an optional distance (km) from the requested point.</summary>
public sealed record StoreDto(
    string Id,
    string Name,
    double Lat,
    double Lng,
    double? DistanceKm);
