namespace Mach.Indexer.Functions.Indexing;

/// <summary>
/// Logical cache-key prefixes used for bulk invalidation via
/// <see cref="Mach.Application.Ports.ICacheStore.RemoveByPrefixAsync(string, System.Threading.CancellationToken)"/>.
/// </summary>
/// <remarks>
/// CONVENTION (must stay in sync with the BFF's cache-aside keys): every cache entry the BFF
/// writes is namespaced under one of these logical prefixes, e.g.
/// <list type="bullet">
///   <item><c>product:{slug}</c> — a single product-detail (PDP) read.</item>
///   <item><c>catalog:{...}</c> — category trees, listing/search facet payloads, navigation.</item>
///   <item><c>content:{contentType}:{slug}</c> — CMS content entries (Contentstack).</item>
/// </list>
/// The Indexer never reads these keys; it only evicts whole prefixes when an upstream change
/// event arrives so the BFF re-populates the cache from source on the next request. If the BFF
/// changes a prefix, update it here too.
/// </remarks>
public static class CachePrefixes
{
    /// <summary>Single product-detail reads (e.g. <c>product:{slug}</c>).</summary>
    public const string Product = "product:";

    /// <summary>Catalog-shaped reads: categories, listings, navigation (e.g. <c>catalog:{...}</c>).</summary>
    public const string Catalog = "catalog:";

    /// <summary>CMS content reads (e.g. <c>content:{contentType}:{slug}</c>).</summary>
    public const string Content = "content:";
}
