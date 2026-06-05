using Algolia.Search.Models.Search;

using static Mach.Infrastructure.Algolia.AlgoliaRecordMapper;

namespace Mach.Infrastructure.Algolia;

/// <summary>
/// Builds the <see cref="IndexSettings"/> applied to the product index. Settings are applied on
/// full reindex so the index schema stays in sync with the mapped record shape.
/// </summary>
public static class AlgoliaIndexSettings
{
    /// <summary>
    /// Constructs the canonical product index settings: searchable attributes (ordered by weight),
    /// attributes available for faceting/filtering, and a custom ranking that prefers in-stock items.
    /// </summary>
    public static IndexSettings Build() => new()
    {
        SearchableAttributes =
        [
            // Order conveys weight: name is most important, then categories, then description.
            Attributes.Name,
            Attributes.Categories,
            $"unordered({Attributes.Description})",
            Attributes.Sku,
        ],
        AttributesForFaceting =
        [
            // searchable() lets the storefront build a facet search box over brand.
            "searchable(brand)",
            "category",
            "color",
            "size",
            Attributes.Currency,
            // filterOnly keeps stock out of the visible facet list but still filterable.
            $"filterOnly({Attributes.InStock})",
        ],
        CustomRanking =
        [
            // In-stock first, then cheapest.
            $"desc({Attributes.InStock})",
            $"asc({Attributes.Price})",
        ],
        AttributesToRetrieve =
        [
            Attributes.ObjectId,
            Attributes.Sku,
            Attributes.Slug,
            Attributes.Name,
            Attributes.Description,
            Attributes.Price,
            Attributes.Currency,
            Attributes.Categories,
            Attributes.InStock,
        ],
    };
}
