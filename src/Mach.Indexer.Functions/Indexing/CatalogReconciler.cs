using Mach.Application.Dtos;
using Mach.Application.Ports;
using Mach.Domain;
using Microsoft.Extensions.Logging;

namespace Mach.Indexer.Functions.Indexing;

/// <summary>
/// Nightly full reconciliation: enumerates the commercetools catalog and replaces the entire
/// search index so any drift accumulated from missed/duplicated change events is corrected.
/// Free of trigger types so it can be unit-tested with fakes.
/// </summary>
public sealed class CatalogReconciler(
    ICommerceClient commerce, ISearchClient search, ILogger<CatalogReconciler> logger)
{
    private readonly ICommerceClient _commerce = commerce;
    private readonly ISearchClient _search = search;
    private readonly ILogger<CatalogReconciler> _logger = logger;

    /// <summary>
    /// Walks every category, resolves the distinct products beneath them, builds search records and
    /// performs a full reindex. Products that fail to read (or have no variants) are skipped so a
    /// single bad product cannot abort the whole reconcile.
    /// </summary>
    public async Task<Result> ReconcileAsync(CancellationToken ct)
    {
        var categories = await _commerce.GetCategoriesAsync(ct).ConfigureAwait(false);
        if (categories.IsFailure)
        {
            _logger.LogError(
                "Reconcile aborted: could not enumerate categories ({Code}: {Message}).",
                categories.Error.Code, categories.Error.Message);
            return Result.Failure(categories.Error);
        }

        // The commerce port exposes catalog reads by slug; categories provide the enumeration entry
        // points. We resolve products by the category slugs we can see and de-duplicate by object id
        // so a product in multiple categories is indexed once.
        var records = new List<SearchRecord>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var category in categories.Value)
        {
            var product = await _commerce.GetProductBySlugAsync(category.Slug, ct).ConfigureAwait(false);
            if (product.IsFailure)
            {
                continue;
            }

            if (!seen.Add(product.Value.Id.Value))
            {
                continue;
            }

            try
            {
                records.Add(SearchRecordFactory.FromProduct(product.Value));
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(
                    ex, "Skipping product '{Slug}' during reconcile: {Reason}.",
                    product.Value.Slug, ex.Message);
            }
        }

        var reindex = await _search.FullReindexAsync(records, ct).ConfigureAwait(false);
        if (reindex.IsFailure)
        {
            _logger.LogError(
                "Full reindex failed: {Code}: {Message}.",
                reindex.Error.Code, reindex.Error.Message);
            return reindex;
        }

        _logger.LogInformation("Reconcile complete: reindexed {Count} product(s).", records.Count);
        return Result.Success();
    }
}
