using Algolia.Search.Exceptions;

using Mach.Application.Dtos;
using Mach.Application.Ports;
using Mach.Domain;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using AlgoliaClient = Algolia.Search.Clients.ISearchClient;

namespace Mach.Infrastructure.Algolia;

/// <summary>
/// Algolia-backed implementation of the application's <see cref="ISearchClient"/> port. Performs
/// indexing-only write operations using the admin API key; all failures are translated to
/// <see cref="Result"/> rather than thrown.
/// </summary>
public sealed class AlgoliaSearchClient : ISearchClient
{
    private readonly AlgoliaClient _client;
    private readonly AlgoliaOptions _options;
    private readonly ILogger<AlgoliaSearchClient> _logger;

    public AlgoliaSearchClient(
        AlgoliaClient client,
        IOptions<AlgoliaOptions> options,
        ILogger<AlgoliaSearchClient> logger)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _options = (options ?? throw new ArgumentNullException(nameof(options))).Value;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    private string IndexName => _options.IndexName;

    public Task<Result> IndexProductAsync(SearchRecord record, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(record);

        IReadOnlyDictionary<string, object?> body;
        try
        {
            body = AlgoliaRecordMapper.ToRecord(record);
        }
        catch (ArgumentException ex)
        {
            return Task.FromResult(Result.Failure(Error.Validation(ex.Message)));
        }

        return ExecuteAsync(
            $"index product '{record.ObjectId}'",
            () => _client.SaveObjectAsync(IndexName, body, options: null, cancellationToken: ct),
            ct);
    }

    public Task<Result> PartialUpdateProductAsync(
        string objectId, IReadOnlyDictionary<string, object?> changes, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(objectId))
        {
            return Task.FromResult(Result.Failure(Error.Validation("objectId must be provided.")));
        }

        ArgumentNullException.ThrowIfNull(changes);

        if (changes.Count == 0)
        {
            return Task.FromResult(Result.Failure(Error.Validation("No attributes supplied to update.")));
        }

        // Carry objectID in the payload so Algolia targets the right record.
        var attributes = new Dictionary<string, object?>(changes, StringComparer.Ordinal)
        {
            [AlgoliaRecordMapper.Attributes.ObjectId] = objectId,
        };

        return ExecuteAsync(
            $"partial-update product '{objectId}'",
            () => _client.PartialUpdateObjectAsync(
                IndexName, objectId, attributes, createIfNotExists: false, options: null, cancellationToken: ct),
            ct);
    }

    public Task<Result> DeleteProductAsync(string objectId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(objectId))
        {
            return Task.FromResult(Result.Failure(Error.Validation("objectId must be provided.")));
        }

        return ExecuteAsync(
            $"delete product '{objectId}'",
            () => _client.DeleteObjectAsync(IndexName, objectId, options: null, cancellationToken: ct),
            ct);
    }

    public async Task<Result> FullReindexAsync(IReadOnlyList<SearchRecord> records, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(records);

        List<IReadOnlyDictionary<string, object?>> bodies;
        try
        {
            bodies = records.Select(AlgoliaRecordMapper.ToRecord).ToList();
        }
        catch (ArgumentException ex)
        {
            return Result.Failure(Error.Validation(ex.Message));
        }

        // Apply index settings first so the (possibly fresh) index has the right schema, then
        // atomically replace all objects.
        var settingsResult = await ExecuteAsync(
            "apply index settings",
            () => _client.SetSettingsAsync(
                IndexName, AlgoliaIndexSettings.Build(), forwardToReplicas: null, options: null, cancellationToken: ct),
            ct).ConfigureAwait(false);

        if (settingsResult.IsFailure)
        {
            return settingsResult;
        }

        return await ExecuteAsync(
            $"full reindex of {bodies.Count} record(s)",
            () => _client.ReplaceAllObjectsAsync(IndexName, bodies, cancellationToken: ct),
            ct).ConfigureAwait(false);
    }

    private async Task<Result> ExecuteAsync(string operation, Func<Task> action, CancellationToken ct)
    {
        try
        {
            ct.ThrowIfCancellationRequested();
            await action().ConfigureAwait(false);
            return Result.Success();
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (AlgoliaApiException ex)
        {
            _logger.LogError(ex, "Algolia API error during {Operation} (HTTP {Status}).", operation, ex.HttpErrorCode);
            var error = ex.HttpErrorCode is 404
                ? Error.NotFound($"Algolia could not {operation}: {ex.Message}")
                : ex.HttpErrorCode is 409
                    ? Error.Conflict($"Algolia conflict during {operation}: {ex.Message}")
                    : Error.Unexpected($"Algolia API failure during {operation}: {ex.Message}");
            return Result.Failure(error);
        }
        catch (AlgoliaUnreachableHostException ex)
        {
            _logger.LogError(ex, "Algolia unreachable during {Operation}.", operation);
            return Result.Failure(Error.Unexpected($"Algolia unreachable during {operation}: {ex.Message}"));
        }
        catch (AlgoliaException ex)
        {
            _logger.LogError(ex, "Algolia error during {Operation}.", operation);
            return Result.Failure(Error.Unexpected($"Algolia error during {operation}: {ex.Message}"));
        }
    }
}
