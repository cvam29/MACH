namespace Mach.Application.Ports;

/// <summary>
/// A pending transactional-outbox row, ready to be published to the message bus.
/// </summary>
public sealed record OutboxMessage(Guid Id, string Topic, string Type, string Payload);

/// <summary>
/// Reads and acknowledges transactional-outbox rows. Drives the outbox dispatcher
/// (<c>Mach.Outbox.Functions</c>), which publishes via <see cref="IMessageBus"/>.
/// Implemented by <c>Mach.Persistence</c>.
/// </summary>
public interface IOutboxReader
{
    /// <summary>Fetch up to <paramref name="batchSize"/> unsent messages, oldest first.</summary>
    Task<IReadOnlyList<OutboxMessage>> GetUnsentAsync(int batchSize, CancellationToken ct);

    /// <summary>Mark a message as successfully published.</summary>
    Task MarkSentAsync(Guid id, CancellationToken ct);

    /// <summary>Record a failed publish attempt (increments attempts, stores the error).</summary>
    Task MarkFailedAsync(Guid id, string error, CancellationToken ct);
}
