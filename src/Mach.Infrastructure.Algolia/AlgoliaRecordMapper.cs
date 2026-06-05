using Mach.Application.Dtos;

namespace Mach.Infrastructure.Algolia;

/// <summary>
/// Maps application <see cref="SearchRecord"/> DTOs to flat Algolia records (a string-keyed
/// dictionary). Facets are hoisted to top-level attributes so they can be configured for
/// faceting and filtering in the index settings.
/// </summary>
public static class AlgoliaRecordMapper
{
    /// <summary>Top-level attribute names produced by the mapper.</summary>
    public static class Attributes
    {
        public const string ObjectId = "objectID";
        public const string Sku = "sku";
        public const string Slug = "slug";
        public const string Name = "name";
        public const string Description = "description";
        public const string Price = "price";
        public const string Currency = "currency";
        public const string Categories = "categories";
        public const string InStock = "inStock";
    }

    /// <summary>
    /// Flattens a <see cref="SearchRecord"/> into an Algolia record. The returned dictionary is
    /// ordered and uses the attribute names in <see cref="Attributes"/>; each entry in
    /// <see cref="SearchRecord.Facets"/> becomes its own top-level attribute.
    /// </summary>
    /// <exception cref="ArgumentNullException">When <paramref name="record"/> is null.</exception>
    /// <exception cref="ArgumentException">
    /// When <see cref="SearchRecord.ObjectId"/> is null/whitespace, or a facet key collides with a
    /// reserved attribute name.
    /// </exception>
    public static IReadOnlyDictionary<string, object?> ToRecord(SearchRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);

        if (string.IsNullOrWhiteSpace(record.ObjectId))
        {
            throw new ArgumentException("SearchRecord.ObjectId must be a non-empty value.", nameof(record));
        }

        var map = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            [Attributes.ObjectId] = record.ObjectId,
            [Attributes.Sku] = record.Sku.Value,
            [Attributes.Slug] = record.Slug,
            [Attributes.Name] = record.Name,
            [Attributes.Description] = record.Description,
            [Attributes.Price] = record.Price.Amount,
            [Attributes.Currency] = record.Price.Currency,
            [Attributes.Categories] = record.Categories is null
                ? new List<string>()
                : new List<string>(record.Categories),
            [Attributes.InStock] = record.InStock,
        };

        if (record.Facets is not null)
        {
            foreach (var (key, value) in record.Facets)
            {
                if (string.IsNullOrWhiteSpace(key))
                {
                    throw new ArgumentException("Facet keys must be non-empty.", nameof(record));
                }

                if (map.ContainsKey(key))
                {
                    throw new ArgumentException(
                        $"Facet key '{key}' collides with a reserved record attribute.", nameof(record));
                }

                map[key] = value;
            }
        }

        return map;
    }
}
