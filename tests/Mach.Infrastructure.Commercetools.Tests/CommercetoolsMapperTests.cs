using commercetools.Sdk.Api.Models.Carts;
using commercetools.Sdk.Api.Models.Categories;
using commercetools.Sdk.Api.Models.Common;
using commercetools.Sdk.Api.Models.Orders;
using commercetools.Sdk.Api.Models.Products;

using Mach.Domain;
using Mach.Infrastructure.Commercetools;

using Shouldly;

namespace Mach.Infrastructure.Commercetools.Tests;

public sealed class CommercetoolsMapperTests
{
    private readonly CommercetoolsMapper _mapper = new("en");

    [Fact]
    public void MapMoney_converts_cent_precision_to_decimal()
    {
        var money = new CentPrecisionMoney { CentAmount = 1999, CurrencyCode = "EUR", FractionDigits = 2 };

        var result = CommercetoolsMapper.MapMoney(money);

        result.Amount.ShouldBe(19.99m);
        result.Currency.ShouldBe("EUR");
    }

    [Fact]
    public void Localize_prefers_exact_locale_then_language_then_any()
    {
        var exact = new LocalizedString { ["en"] = "Hello", ["de"] = "Hallo" };
        _mapper.Localize(exact).ShouldBe("Hello");

        var languageOnly = new LocalizedString { ["en-US"] = "Color", ["de"] = "Farbe" };
        _mapper.Localize(languageOnly).ShouldBe("Color");

        var fallback = new LocalizedString { ["fr"] = "Bonjour" };
        _mapper.Localize(fallback).ShouldBe("Bonjour");
    }

    [Fact]
    public void MapCategory_maps_id_slug_name_and_parent()
    {
        var category = new Category
        {
            Id = "cat-1",
            Slug = new LocalizedString { ["en"] = "boots" },
            Name = new LocalizedString { ["en"] = "Boots" },
            Parent = new CategoryReference { Id = "parent-1" },
        };

        var dto = _mapper.MapCategory(category);

        dto.Id.ShouldBe("cat-1");
        dto.Slug.ShouldBe("boots");
        dto.Name.ShouldBe("Boots");
        dto.ParentId.ShouldBe("parent-1");
    }

    [Fact]
    public void MapProduct_maps_master_variant_price_and_attributes()
    {
        var product = new ProductProjection
        {
            Id = "prod-1",
            Slug = new LocalizedString { ["en"] = "running-shoe" },
            Name = new LocalizedString { ["en"] = "Running Shoe" },
            Description = new LocalizedString { ["en"] = "Fast." },
            Categories = [new CategoryReference { Id = "cat-9" }],
            MasterVariant = new ProductVariant
            {
                Sku = "SKU-1",
                Price = new Price
                {
                    Value = new CentPrecisionMoney { CentAmount = 12000, CurrencyCode = "EUR", FractionDigits = 2 },
                },
                Attributes = [new commercetools.Sdk.Api.Models.Products.Attribute { Name = "color", Value = "red" }],
                Images = [new Image { Url = "https://img/1.png" }],
                Availability = new ProductVariantAvailability { IsOnStock = true },
            },
        };

        var dto = _mapper.MapProduct(product);

        dto.Id.Value.ShouldBe("prod-1");
        dto.Slug.ShouldBe("running-shoe");
        dto.CategoryIds.ShouldBe(["cat-9"]);
        dto.Variants.Count.ShouldBe(1);
        var variant = dto.Variants[0];
        variant.Sku.Value.ShouldBe("SKU-1");
        variant.Price.Amount.ShouldBe(120m);
        variant.Attributes["color"].ShouldBe("red");
        variant.ImageUrls.ShouldBe(["https://img/1.png"]);
        variant.InStock.ShouldBeTrue();
    }

    [Fact]
    public void MapCart_carries_version_and_line_items()
    {
        var cart = new Cart
        {
            Id = "cart-1",
            Version = 7,
            TotalPrice = new CentPrecisionMoney { CentAmount = 5000, CurrencyCode = "EUR", FractionDigits = 2 },
            AnonymousId = "anon-9",
            LineItems =
            [
                new LineItem
                {
                    Id = "li-1",
                    Name = new LocalizedString { ["en"] = "Shoe" },
                    Quantity = 2,
                    Variant = new ProductVariant { Sku = "SKU-1" },
                    Price = new Price
                    {
                        Value = new CentPrecisionMoney { CentAmount = 2500, CurrencyCode = "EUR", FractionDigits = 2 },
                    },
                    TotalPrice = new CentPrecisionMoney { CentAmount = 5000, CurrencyCode = "EUR", FractionDigits = 2 },
                },
            ],
        };

        var dto = _mapper.MapCart(cart);

        dto.Id.Value.ShouldBe("cart-1");
        dto.Version.ShouldBe(7);
        dto.Currency.ShouldBe("EUR");
        dto.AnonymousId.ShouldBe("anon-9");
        dto.TotalPrice.Amount.ShouldBe(50m);
        dto.LineItems.Count.ShouldBe(1);
        dto.LineItems[0].Id.ShouldBe("li-1");
        dto.LineItems[0].Quantity.ShouldBe(2);
        dto.LineItems[0].Sku.Value.ShouldBe("SKU-1");
        dto.LineItems[0].UnitPrice.Amount.ShouldBe(25m);
        dto.LineItems[0].TotalPrice.Amount.ShouldBe(50m);
    }

    [Fact]
    public void MapOrder_collapses_states_to_domain_status()
    {
        var order = new Order
        {
            Id = "order-1",
            OrderNumber = "1001",
            Version = 1,
            TotalPrice = new CentPrecisionMoney { CentAmount = 1000, CurrencyCode = "EUR", FractionDigits = 2 },
            OrderState = IOrderState.Confirmed,
            ShipmentState = IShipmentState.Shipped,
            PaymentState = IPaymentState.Paid,
            CreatedAt = DateTime.UtcNow,
            LineItems = [],
        };

        var dto = _mapper.MapOrder(order);

        dto.OrderNumber.ShouldBe("1001");
        dto.Status.ShouldBe(OrderStatus.Shipped);
        dto.PaymentStatus.ShouldBe(PaymentStatus.Captured);
    }

    [Fact]
    public void MapAddress_round_trips_core_fields()
    {
        var domain = new Mach.Domain.ValueObjects.Address(
            Street: "Main 1", City: "Berlin", PostalCode: "10115", Country: "DE",
            State: "BE", FirstName: "Ada", LastName: "Byte");

        var ct = CommercetoolsMapper.MapAddress(domain);
        ct.StreetName.ShouldBe("Main 1");
        ct.City.ShouldBe("Berlin");
        ct.PostalCode.ShouldBe("10115");
        ct.Country.ShouldBe("DE");

        var back = CommercetoolsMapper.MapAddress((IBaseAddress)ct);
        back.ShouldNotBeNull();
        back!.Value.City.ShouldBe("Berlin");
        back.Value.Country.ShouldBe("DE");
        back.Value.FirstName.ShouldBe("Ada");
    }
}
