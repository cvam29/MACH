using Mach.Application.Ports;
using Microsoft.Extensions.Logging;

namespace Mach.Indexer.Functions.Indexing;

/// <summary>
/// Evicts the BFF's cache-aside entries by logical prefix when an upstream change event arrives.
/// Kept free of Service Bus types so it is unit-testable with a fake <see cref="ICacheStore"/>.
/// </summary>
public sealed class CacheInvalidator
{
    private readonly ICacheStore _cache;
    private readonly ILogger<CacheInvalidator> _logger;

    public CacheInvalidator(ICacheStore cache, ILogger<CacheInvalidator> logger)
    {
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Invalidate everything affected by a product change: the product-detail entry and any
    /// catalog-shaped reads (listings/categories) that may include the product.
    /// </summary>
    public async Task InvalidateProductCacheAsync(CancellationToken ct)
    {
        await _cache.RemoveByPrefixAsync(CachePrefixes.Product, ct).ConfigureAwait(false);
        await _cache.RemoveByPrefixAsync(CachePrefixes.Catalog, ct).ConfigureAwait(false);
        _logger.LogInformation(
            "Invalidated cache prefixes '{ProductPrefix}' and '{CatalogPrefix}'.",
            CachePrefixes.Product, CachePrefixes.Catalog);
    }

    /// <summary>Invalidate CMS content reads.</summary>
    public async Task InvalidateContentCacheAsync(CancellationToken ct)
    {
        await _cache.RemoveByPrefixAsync(CachePrefixes.Content, ct).ConfigureAwait(false);
        _logger.LogInformation("Invalidated cache prefix '{ContentPrefix}'.", CachePrefixes.Content);
    }
}
