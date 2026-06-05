using Mach.Domain.ValueObjects;

namespace Mach.Application.Dtos;

/// <summary>
/// A flattened search record pushed to the search index (Algolia). Storefront search
/// itself runs browser-side; the platform only owns indexing.
/// </summary>
public sealed record SearchRecord(
    string ObjectId,
    Sku Sku,
    string Slug,
    string Name,
    string Description,
    Money Price,
    IReadOnlyList<string> Categories,
    IReadOnlyDictionary<string, string> Facets,
    bool InStock);
