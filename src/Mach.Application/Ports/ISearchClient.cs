using Mach.Application.Dtos;
using Mach.Domain;
using Mach.Domain.ValueObjects;

namespace Mach.Application.Ports;

/// <summary>
/// Port over the search index (Algolia) — indexing only. Storefront search runs browser-side
/// with a search-only key. Implemented by <c>Mach.Infrastructure.Algolia</c>.
/// </summary>
public interface ISearchClient
{
    Task<Result> IndexProductAsync(SearchRecord record, CancellationToken ct);

    Task<Result> PartialUpdateProductAsync(
        string objectId, IReadOnlyDictionary<string, object?> changes, CancellationToken ct);

    Task<Result> DeleteProductAsync(string objectId, CancellationToken ct);

    /// <summary>Replace the entire index with the supplied records (nightly reconciliation).</summary>
    Task<Result> FullReindexAsync(IReadOnlyList<SearchRecord> records, CancellationToken ct);
}
