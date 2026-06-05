using Mach.Domain.ValueObjects;

namespace Mach.Application.Dtos;

/// <summary>A catalog category node (with optional parent for tree building).</summary>
public sealed record CategoryDto(
    string Id,
    string Slug,
    string Name,
    string? ParentId);

/// <summary>A single purchasable product variant.</summary>
public sealed record ProductVariantDto(
    Sku Sku,
    Money Price,
    Money? ListPrice,
    IReadOnlyDictionary<string, string> Attributes,
    IReadOnlyList<string> ImageUrls,
    bool InStock);

/// <summary>A product as exposed by the commerce engine (commercetools).</summary>
public sealed record ProductDto(
    ProductId Id,
    string Slug,
    string Name,
    string Description,
    IReadOnlyList<string> CategoryIds,
    IReadOnlyList<ProductVariantDto> Variants);
