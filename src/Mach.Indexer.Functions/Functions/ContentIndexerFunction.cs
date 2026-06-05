using System.Text.Json;
using Mach.Contracts;
using Mach.Indexer.Functions.Indexing;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace Mach.Indexer.Functions.Functions;

/// <summary>
/// Service Bus trigger on the <c>content</c> topic (subscription <c>indexer-content</c>). Reacts to
/// <see cref="ContentChanged"/> events: invalidate the content cache and, when the content maps to a
/// product PDP, reindex that product.
/// </summary>
public sealed class ContentIndexerFunction
{
    private const string Topic = "content";
    private const string Subscription = "indexer-content";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly ProductIndexer _indexer;
    private readonly ILogger<ContentIndexerFunction> _logger;

    public ContentIndexerFunction(ProductIndexer indexer, ILogger<ContentIndexerFunction> logger)
    {
        _indexer = indexer ?? throw new ArgumentNullException(nameof(indexer));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [Function(nameof(ContentIndexerFunction))]
    public async Task RunAsync(
        [ServiceBusTrigger(Topic, Subscription, Connection = "ServiceBusConnection")]
        string body,
        CancellationToken ct)
    {
        var @event = JsonSerializer.Deserialize<ContentChanged>(body, JsonOptions)
            ?? throw new InvalidOperationException("Received an empty ContentChanged message.");

        _logger.LogInformation(
            "Handling ContentChanged {Kind} for '{ContentType}' (slug '{Slug}').",
            @event.Kind, @event.ContentType, @event.Slug);

        await _indexer.HandleContentChangedAsync(@event, ct).ConfigureAwait(false);
    }
}
