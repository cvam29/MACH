using commercetools.Sdk.Api.Models.Carts;
using commercetools.Sdk.Api.Models.Categories;
using commercetools.Sdk.Api.Models.Common;
using commercetools.Sdk.Api.Models.Customers;
using commercetools.Sdk.Api.Models.Orders;
using commercetools.Sdk.Api.Models.Products;

using Mach.Application.Dtos;
using Mach.Domain;
using Mach.Domain.ValueObjects;

namespace Mach.Infrastructure.Commercetools;

/// <summary>
/// Pure (stateless) translation between commercetools API models and the application DTOs.
/// Kept free of I/O so the mapping paths can be unit-tested directly.
/// </summary>
internal sealed class CommercetoolsMapper(string defaultLocale)
{
    private readonly string _locale = defaultLocale;

    /// <summary>Project a commercetools <see cref="IMoney"/> (cent precision) to a domain <see cref="Money"/>.</summary>
    public static Money MapMoney(IMoney? money)
    {
        if (money is null)
        {
            return Money.Zero("EUR");
        }

        var fractionDigits = money is ICentPrecisionMoney cp ? cp.FractionDigits : 2;
        var factor = (decimal)Math.Pow(10, fractionDigits);
        return new Money(money.CentAmount / factor, money.CurrencyCode);
    }

    public string Localize(ILocalizedString? value)
    {
        if (value is null || value.Count == 0)
        {
            return string.Empty;
        }

        if (value.TryGetValue(_locale, out var exact) && !string.IsNullOrEmpty(exact))
        {
            return exact;
        }

        // Fall back to a language-only match (e.g. "en" for "en-US") then any translation.
        var prefix = _locale.Split('-')[0];
        foreach (var kvp in value)
        {
            if (kvp.Key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return kvp.Value;
            }
        }

        return value.Values.First();
    }

    public static Address? MapAddress(IBaseAddress? address)
    {
        if (address is null)
        {
            return null;
        }

        var street = string.Join(' ', new[] { address.StreetName, address.StreetNumber }
            .Where(s => !string.IsNullOrWhiteSpace(s))).Trim();

        return new Address(
            Street: street,
            City: address.City ?? string.Empty,
            PostalCode: address.PostalCode ?? string.Empty,
            Country: address.Country ?? string.Empty,
            State: address.State ?? address.Region,
            FirstName: address.FirstName,
            LastName: address.LastName);
    }

    /// <summary>Map a domain <see cref="Address"/> to a commercetools address draft.</summary>
    public static CtBaseAddress MapAddress(Address address) => new()
    {
        StreetName = address.Street,
        City = address.City,
        PostalCode = address.PostalCode,
        Country = address.Country,
        State = address.State,
        FirstName = address.FirstName,
        LastName = address.LastName,
    };

    public CategoryDto MapCategory(ICategory category) => new(
        Id: category.Id,
        Slug: Localize(category.Slug),
        Name: Localize(category.Name),
        ParentId: category.Parent?.Id);

    public ProductDto MapProduct(IProductProjection product)
    {
        var variants = new List<ProductVariantDto>();
        if (product.MasterVariant is not null)
        {
            variants.Add(MapVariant(product.MasterVariant));
        }

        if (product.Variants is not null)
        {
            variants.AddRange(product.Variants.Select(MapVariant));
        }

        var categoryIds = product.Categories?.Select(c => c.Id).ToList() ?? [];

        return new ProductDto(
            Id: new ProductId(product.Id),
            Slug: Localize(product.Slug),
            Name: Localize(product.Name),
            Description: Localize(product.Description),
            CategoryIds: categoryIds,
            Variants: variants);
    }

    private static ProductVariantDto MapVariant(IProductVariant variant)
    {
        var price = variant.Price?.Value is { } value ? MapMoney(value) : Money.Zero("EUR");
        Money? listPrice = variant.Price?.Discounted is not null
            ? MapMoney(variant.Price.Value)
            : null;

        var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (variant.Attributes is not null)
        {
            foreach (var attr in variant.Attributes)
            {
                attributes[attr.Name] = attr.Value?.ToString() ?? string.Empty;
            }
        }

        var images = variant.Images?.Select(i => i.Url).Where(u => !string.IsNullOrEmpty(u)).ToList() ?? [];
        var inStock = variant.Availability?.IsOnStock ?? true;

        return new ProductVariantDto(
            Sku: new Sku(variant.Sku ?? string.Empty),
            Price: price,
            ListPrice: listPrice,
            Attributes: attributes,
            ImageUrls: images,
            InStock: inStock);
    }

    public CartDto MapCart(ICart cart)
    {
        var lineItems = (cart.LineItems ?? [])
            .Select(MapLineItem)
            .ToList();

        return new CartDto(
            Id: new CartId(cart.Id),
            Version: cart.Version,
            Currency: cart.TotalPrice?.CurrencyCode ?? string.Empty,
            LineItems: lineItems,
            TotalPrice: MapMoney(cart.TotalPrice),
            ShippingAddress: MapAddress(cart.ShippingAddress),
            BillingAddress: MapAddress(cart.BillingAddress),
            ShippingMethodId: cart.ShippingInfo?.ShippingMethod?.Id,
            CustomerId: cart.CustomerId is { Length: > 0 } cid ? new CustomerId(cid) : null,
            AnonymousId: cart.AnonymousId);
    }

    private LineItemDto MapLineItem(ILineItem item) => new(
        Id: item.Id,
        Sku: new Sku(item.Variant?.Sku ?? string.Empty),
        Name: Localize(item.Name),
        Quantity: (int)item.Quantity,
        UnitPrice: MapMoney(item.Price?.Value),
        TotalPrice: MapMoney(item.TotalPrice));

    public OrderDto MapOrder(IOrder order)
    {
        var lines = (order.LineItems ?? [])
            .Select(li => new OrderLineDto(
                Sku: new Sku(li.Variant?.Sku ?? string.Empty),
                Name: Localize(li.Name),
                Quantity: (int)li.Quantity,
                UnitPrice: MapMoney(li.Price?.Value),
                TotalPrice: MapMoney(li.TotalPrice)))
            .ToList();

        return new OrderDto(
            Id: new OrderId(order.Id),
            OrderNumber: order.OrderNumber ?? string.Empty,
            CustomerId: order.CustomerId is { Length: > 0 } cid ? new CustomerId(cid) : null,
            Status: MapOrderStatus(order),
            PaymentStatus: MapPaymentStatus(order),
            TotalPrice: MapMoney(order.TotalPrice),
            Lines: lines,
            ShippingAddress: MapAddress(order.ShippingAddress),
            DeliveryType: null,
            CreatedAt: order.CreatedAt);
    }

    public CustomerDto MapCustomer(ICustomer customer)
    {
        var addresses = (customer.Addresses ?? [])
            .Select(MapAddress)
            .Where(a => a is not null)
            .Select(a => a!.Value)
            .ToList();

        return new CustomerDto(
            Id: new CustomerId(customer.Id),
            Email: customer.Email ?? string.Empty,
            FirstName: customer.FirstName ?? string.Empty,
            LastName: customer.LastName ?? string.Empty,
            Addresses: addresses);
    }

    /// <summary>
    /// Map a commercetools order to the domain <see cref="OrderStatus"/>. commercetools models order
    /// lifecycle across <c>orderState</c> and <c>shipmentState</c>; we collapse them to our enum.
    /// </summary>
    private static OrderStatus MapOrderStatus(IOrder order)
    {
        var orderState = order.OrderState?.Value;
        var shipmentState = order.ShipmentState?.Value;

        // Cancelled is terminal regardless of shipment.
        if (orderState == OrderState.Cancelled)
        {
            return OrderStatus.Cancelled;
        }

        if (shipmentState == ShipmentState.Delivered)
        {
            return OrderStatus.Delivered;
        }

        if (shipmentState == ShipmentState.Shipped)
        {
            return OrderStatus.Shipped;
        }

        if (orderState == OrderState.Confirmed)
        {
            return OrderStatus.Fulfilling;
        }

        if (order.PaymentState?.Value == PaymentState.Paid)
        {
            return OrderStatus.Paid;
        }

        return OrderStatus.Pending;
    }

    private static PaymentStatus MapPaymentStatus(IOrder order)
    {
        return order.PaymentState?.Value switch
        {
            PaymentState.Paid => PaymentStatus.Captured,
            PaymentState.CreditOwed => PaymentStatus.Refunded,
            PaymentState.Failed => PaymentStatus.Refused,
            PaymentState.Pending => PaymentStatus.Pending,
            PaymentState.BalanceDue => PaymentStatus.Authorized,
            _ => PaymentStatus.Pending,
        };
    }
}
