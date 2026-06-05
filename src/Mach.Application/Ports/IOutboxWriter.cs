namespace Mach.Application.Ports;

/// <summary>
/// Enqueues an integration event into the transactional outbox within the current unit of work.
/// The event is published after the surrounding EF transaction commits.
/// Implemented by <c>Mach.Persistence</c>.
/// </summary>
public interface IOutboxWriter
{
    Task EnqueueAsync<TEvent>(string topic, TEvent @event, CancellationToken ct)
        where TEvent : notnull;
}
