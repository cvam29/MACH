using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace Mach.Outbox.Functions;

/// <summary>
/// Timer-triggered host function that periodically drains the transactional outbox by delegating
/// to <see cref="OutboxDispatcher"/>. Runs every 10 seconds.
/// </summary>
public sealed class OutboxDispatchFunction
{
    private readonly OutboxDispatcher _dispatcher;
    private readonly ILogger<OutboxDispatchFunction> _logger;

    public OutboxDispatchFunction(OutboxDispatcher dispatcher, ILogger<OutboxDispatchFunction> logger)
    {
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [Function(nameof(OutboxDispatchFunction))]
    public async Task RunAsync(
        [TimerTrigger("*/10 * * * * *")] TimerInfo timer,
        CancellationToken ct)
    {
        try
        {
            await _dispatcher.DispatchPendingAsync(ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Host shutting down between ticks — nothing to do; rows retry on next start.
        }
        catch (Exception ex)
        {
            // The batch read itself failed (e.g. SQL unavailable). Log and let the next tick retry;
            // throwing would only surface a noisy unhandled-invocation error.
            _logger.LogError(ex, "Outbox dispatch tick failed before draining the batch.");
        }
    }
}
