using Mach.Application.Dtos;
using Mach.Application.Ports;
using Mach.Contracts;
using Microsoft.Extensions.Logging;

namespace Mach.Indexer.Functions.Indexing;

/// <summary>
/// Keeps the search index and the BFF cache in sync with upstream catalog/content changes.
/// All Service Bus concerns live in the trigger functions; this service takes already-deserialized
/// <see cref="ProductChanged"/> / <see cref="ContentChanged"/> events so it is fully unit-testable
/// with fake ports.
/// </summary>
public sealed class ProductIndexer(
    ICommerceClient commerce,
    ISearchClient search,
    ICmsClient cms,
    CacheInvalidator cacheInvalidator,
    ILogger<ProductIndexer> logger)
{
    /// <summary>Content type whose entries enrich a product (PDP marketing copy), keyed by product slug.</summary>
    private const string PdpMarketingContentType = "pdp_marketing_block";

    private readonly ICommerceClient _commerce = commerce;
    private readonly ISearchClient _search = search;
    private readonly ICmsClient _cms = cms;
    private readonly CacheInvalidator _cacheInvalidator = cacheInvalidator;
    private readonly ILogger<ProductIndexer> _logger = logger;

    /// <summary>
    /// Reacts to a <see cref="ProductChanged"/> event: re-index (Created/Updated) or remove
    /// (Deleted) the product, then invalidate the product/catalog caches so the BFF re-reads.
    /// </summary>
    public async Task HandleProductChangedAsync(ProductChanged @event, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(@event);

        switch (@event.Kind)
        {
            case ProductChangeKind.Created:
            case ProductChangeKind.Updated:
                await IndexProductAsync(@event.Slug, ct).ConfigureAwait(false);
                break;

            case ProductChangeKind.Deleted:
                await DeleteProductAsync(@event.ProductId, ct).ConfigureAwait(false);
                break;

            default:
                _logger.LogWarning(
                    "Unhandled ProductChangeKind '{Kind}' for product '{ProductId}'.",
                    @event.Kind, @event.ProductId);
                break;
        }

        // Always invalidate after a catalog mutation, regardless of index outcome, so the BFF
        // cannot keep serving stale product/listing reads.
        await _cacheInvalidator.InvalidateProductCacheAsync(ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Reacts to a <see cref="ContentChanged"/> event: invalidate the content cache and, when the
    /// changed content maps to a product PDP, reindex that product.
    /// </summary>
    public async Task HandleContentChangedAsync(ContentChanged @event, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(@event);

        await _cacheInvalidator.InvalidateContentCacheAsync(ct).ConfigureAwait(false);

        // PDP marketing blocks are keyed by product slug; a change there should refresh the index
        // record (the description/marketing enrichment may have changed). Other content types
        // (navigation, generic pages) only need cache invalidation.
        if (string.Equals(@event.ContentType, PdpMarketingContentType, StringComparison.Ordinal)
            && @event.Kind != ContentChangeKind.Deleted)
        {
            await IndexProductAsync(@event.Slug, ct).ConfigureAwait(false);
            await _cacheInvalidator.InvalidateProductCacheAsync(ct).ConfigureAwait(false);
        }
    }

    private async Task IndexProductAsync(string slug, CancellationToken ct)
    {
        var product = await _commerce.GetProductBySlugAsync(slug, ct).ConfigureAwait(false);
        if (product.IsFailure)
        {
            _logger.LogWarning(
                "Cannot index product '{Slug}': commerce read failed ({Code}: {Message}).",
                slug, product.Error.Code, product.Error.Message);
            return;
        }

        var enriched = await EnrichWithContentAsync(product.Value, ct).ConfigureAwait(false);
        var record = SearchRecordFactory.FromProduct(enriched);

        var index = await _search.IndexProductAsync(record, ct).ConfigureAwait(false);
        if (index.IsFailure)
        {
            _logger.LogError(
                "Failed to index product '{Slug}' ({ObjectId}): {Code}: {Message}.",
                slug, record.ObjectId, index.Error.Code, index.Error.Message);
            return;
        }

        _logger.LogInformation("Indexed product '{Slug}' ({ObjectId}).", slug, record.ObjectId);
    }

    /// <summary>
    /// Best-effort enrichment with CMS marketing copy. Missing content is non-fatal — the commerce
    /// description is used as-is so a CMS outage never blocks indexing.
    /// </summary>
    private async Task<ProductDto> EnrichWithContentAsync(ProductDto product, CancellationToken ct)
    {
        var marketing = await _cms.GetEntryAsync(PdpMarketingContentType, product.Slug, ct)
            .ConfigureAwait(false);

        if (marketing.IsSuccess
            && marketing.Value.Fields.TryGetValue("description", out var description)
            && description is string text
            && !string.IsNullOrWhiteSpace(text))
        {
            return product with { Description = text };
        }

        return product;
    }

    private async Task DeleteProductAsync(string productId, CancellationToken ct)
    {
        var delete = await _search.DeleteProductAsync(productId, ct).ConfigureAwait(false);
        if (delete.IsFailure)
        {
            _logger.LogError(
                "Failed to delete product '{ProductId}' from the index: {Code}: {Message}.",
                productId, delete.Error.Code, delete.Error.Message);
            return;
        }

        _logger.LogInformation("Deleted product '{ProductId}' from the index.", productId);
    }
}
