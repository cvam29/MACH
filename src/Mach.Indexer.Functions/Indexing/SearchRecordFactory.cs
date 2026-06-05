using Mach.Application.Dtos;

namespace Mach.Indexer.Functions.Indexing;

/// <summary>
/// Flattens a commercetools <see cref="ProductDto"/> into the search <see cref="SearchRecord"/>
/// the index expects. The first variant is treated as the canonical/primary variant (price, sku,
/// stock); attribute facets are projected from that variant's attributes.
/// </summary>
public static class SearchRecordFactory
{
    /// <summary>
    /// Builds a <see cref="SearchRecord"/> for <paramref name="product"/>. The record's
    /// <see cref="SearchRecord.ObjectId"/> is the commercetools product id so updates/deletes are
    /// addressable by a stable key.
    /// </summary>
    /// <exception cref="ArgumentNullException">When <paramref name="product"/> is null.</exception>
    /// <exception cref="ArgumentException">When the product has no variants.</exception>
    public static SearchRecord FromProduct(ProductDto product)
    {
        ArgumentNullException.ThrowIfNull(product);

        if (product.Variants.Count == 0)
        {
            throw new ArgumentException(
                $"Product '{product.Id.Value}' has no variants and cannot be indexed.", nameof(product));
        }

        var primary = product.Variants[0];

        return new SearchRecord(
            ObjectId: product.Id.Value,
            Sku: primary.Sku,
            Slug: product.Slug,
            Name: product.Name,
            Description: product.Description,
            Price: primary.Price,
            Categories: product.CategoryIds,
            Facets: primary.Attributes,
            InStock: primary.InStock);
    }
}
