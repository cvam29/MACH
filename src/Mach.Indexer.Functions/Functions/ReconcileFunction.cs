using Mach.Indexer.Functions.Indexing;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace Mach.Indexer.Functions.Functions;

/// <summary>
/// Nightly timer (02:00 UTC) that runs a full catalog reconciliation to correct any drift between
/// commercetools and the search index that change events may have missed. Delegates to
/// <see cref="CatalogReconciler"/>.
/// </summary>
public sealed class ReconcileFunction
{
    // Six-field NCRONTAB: sec min hour day month day-of-week. "0 0 2 * * *" = daily at 02:00.
    private const string NightlySchedule = "0 0 2 * * *";

    private readonly CatalogReconciler _reconciler;
    private readonly ILogger<ReconcileFunction> _logger;

    public ReconcileFunction(CatalogReconciler reconciler, ILogger<ReconcileFunction> logger)
    {
        _reconciler = reconciler ?? throw new ArgumentNullException(nameof(reconciler));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [Function(nameof(ReconcileFunction))]
    public async Task RunAsync([TimerTrigger(NightlySchedule)] TimerInfo timer, CancellationToken ct)
    {
        _logger.LogInformation("Starting nightly catalog reconcile.");

        var result = await _reconciler.ReconcileAsync(ct).ConfigureAwait(false);
        if (result.IsFailure)
        {
            _logger.LogError(
                "Nightly reconcile failed: {Code}: {Message}.", result.Error.Code, result.Error.Message);
            return;
        }

        if (timer.ScheduleStatus is { } status)
        {
            _logger.LogInformation(
                "Nightly reconcile finished. Next run at {Next:u}.", status.Next);
        }
    }
}
