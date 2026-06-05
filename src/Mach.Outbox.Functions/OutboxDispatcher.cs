using Mach.Application.Ports;
using Microsoft.Extensions.Logging;

namespace Mach.Outbox.Functions;

/// <summary>
/// Drains pending transactional-outbox rows and publishes them to the message bus.
/// </summary>
/// <remarks>
/// <para>
/// The dispatch loop reads up to <see cref="BatchSize"/> unsent messages oldest-first via
/// <see cref="IOutboxReader.GetUnsentAsync"/>, publishes each to its topic through
/// <see cref="IMessageBus"/>, then acknowledges it with <see cref="IOutboxReader.MarkSentAsync"/>.
/// </para>
/// <para>
/// A publish (or mark-sent) failure for one message is isolated: the row is recorded as failed via
/// <see cref="IOutboxReader.MarkFailedAsync"/> and the loop continues with the remaining batch, so a
/// single poison message never blocks the others. Rows left unsent are retried on the next timer tick.
/// </para>
/// <para>
/// The outbox payload is already a serialized event string. It is published as-is: the
/// <see cref="IMessageBus"/> body is the raw payload string, which the bus serializes (the
/// in-memory bus round-trips it through JSON; the Service Bus bus carries it as the message body).
/// </para>
/// </remarks>
public sealed class OutboxDispatcher
{
    /// <summary>Maximum number of outbox rows processed per dispatch pass.</summary>
    public const int BatchSize = 50;

    private readonly IOutboxReader _reader;
    private readonly IMessageBus _bus;
    private readonly ILogger<OutboxDispatcher> _logger;

    public OutboxDispatcher(IOutboxReader reader, IMessageBus bus, ILogger<OutboxDispatcher> logger)
    {
        _reader = reader ?? throw new ArgumentNullException(nameof(reader));
        _bus = bus ?? throw new ArgumentNullException(nameof(bus));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Publishes one batch of pending outbox messages. Returns the number successfully published.
    /// </summary>
    public async Task<int> DispatchPendingAsync(CancellationToken ct)
    {
        var messages = await _reader.GetUnsentAsync(BatchSize, ct).ConfigureAwait(false);
        if (messages.Count == 0)
        {
            _logger.LogDebug("Outbox dispatch: no pending messages.");
            return 0;
        }

        var sent = 0;
        var failed = 0;

        foreach (var message in messages)
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                // The payload is an already-serialized event string; publish it as-is to the topic.
                await _bus.PublishAsync(message.Topic, message.Payload, ct).ConfigureAwait(false);
                await _reader.MarkSentAsync(message.Id, ct).ConfigureAwait(false);
                sent++;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                // Host shutdown: stop draining; remaining rows are retried on the next tick.
                throw;
            }
            catch (Exception ex)
            {
                failed++;
                _logger.LogError(
                    ex,
                    "Outbox dispatch: failed to publish message {OutboxId} to topic {Topic} (type {Type}).",
                    message.Id,
                    message.Topic,
                    message.Type);

                // Record the failure and keep draining the rest of the batch.
                await _reader.MarkFailedAsync(message.Id, Describe(ex), ct).ConfigureAwait(false);
            }
        }

        _logger.LogInformation(
            "Outbox dispatch: published {Sent} of {Total} message(s), {Failed} failed.",
            sent,
            messages.Count,
            failed);

        return sent;
    }

    private static string Describe(Exception ex) => $"{ex.GetType().Name}: {ex.Message}";
}
