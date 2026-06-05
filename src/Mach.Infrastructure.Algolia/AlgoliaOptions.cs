using System.ComponentModel.DataAnnotations;

namespace Mach.Infrastructure.Algolia;

/// <summary>
/// Configuration for the Algolia indexing adapter, bound from the <c>Algolia:</c> section.
/// </summary>
/// <remarks>
/// Storefront search runs browser-side with a <em>search-only</em> key; this adapter uses the
/// <see cref="AdminApiKey"/> exclusively for write operations (indexing, settings, reindex).
/// </remarks>
public sealed class AlgoliaOptions
{
    public const string SectionName = "Algolia";

    /// <summary>The Algolia application id.</summary>
    [Required(AllowEmptyStrings = false)]
    public string AppId { get; set; } = string.Empty;

    /// <summary>The admin API key. Used for indexing only — never shipped to the browser.</summary>
    [Required(AllowEmptyStrings = false)]
    public string AdminApiKey { get; set; } = string.Empty;

    /// <summary>The target index name.</summary>
    [Required(AllowEmptyStrings = false)]
    public string IndexName { get; set; } = string.Empty;

    /// <summary>
    /// Optional override host(s) (scheme + host[:port]) used instead of Algolia's default DSN.
    /// Intended for testing against a mock transport; leave empty in production.
    /// </summary>
    public IList<string> CustomHosts { get; set; } = [];

    /// <summary>Per-call write timeout in seconds. Defaults to 30s when unset or non-positive.</summary>
    public int WriteTimeoutSeconds { get; set; } = 30;
}
