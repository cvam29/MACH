using System.Text.Json;
using Mach.Contracts;
using Mach.Indexer.Functions.Indexing;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace Mach.Indexer.Functions.Functions;

/// <summary>
/// Service Bus trigger on the <c>catalog</c> topic (subscription <c>indexer</c>). Reacts to
/// <see cref="ProductChanged"/> events: re-index or remove the product and invalidate the
/// product/catalog caches. The trigger only deserializes and delegates to <see cref="ProductIndexer"/>.
/// </summary>
public sealed class CatalogIndexerFunction
{
    private const string Topic = "catalog";
    private const string Subscription = "indexer";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly ProductIndexer _indexer;
    private readonly ILogger<CatalogIndexerFunction> _logger;

    public CatalogIndexerFunction(ProductIndexer indexer, ILogger<CatalogIndexerFunction> logger)
    {
        _indexer = indexer ?? throw new ArgumentNullException(nameof(indexer));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [Function(nameof(CatalogIndexerFunction))]
    public async Task RunAsync(
        [ServiceBusTrigger(Topic, Subscription, Connection = "ServiceBusConnection")]
        string body,
        CancellationToken ct)
    {
        var @event = JsonSerializer.Deserialize<ProductChanged>(body, JsonOptions)
            ?? throw new InvalidOperationException("Received an empty ProductChanged message.");

        _logger.LogInformation(
            "Handling ProductChanged {Kind} for product '{ProductId}' (slug '{Slug}').",
            @event.Kind, @event.ProductId, @event.Slug);

        await _indexer.HandleProductChangedAsync(@event, ct).ConfigureAwait(false);
    }
}
