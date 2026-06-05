using System.Text.Json;
using Mach.Application.Ports;
using Mach.Persistence.Entities;

namespace Mach.Persistence.Repositories;

/// <summary>
/// Enqueues integration events into <c>messaging.OutboxMessages</c> within the caller's
/// unit of work. The row is added to the tracked <see cref="MachDbContext"/> but NOT saved —
/// it is persisted when the caller commits its own transaction (transactional outbox).
/// </summary>
internal sealed class OutboxWriter(MachDbContext db, TimeProvider time) : IOutboxWriter
{
    public Task EnqueueAsync<TEvent>(string topic, TEvent @event, CancellationToken ct)
        where TEvent : notnull
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(topic);

        var row = new OutboxMessageEntity
        {
            Id = SequentialGuid.NewGuid(),
            OccurredUtc = time.GetUtcNow(),
            Topic = topic,
            Type = @event.GetType().FullName ?? @event.GetType().Name,
            Payload = JsonSerializer.Serialize(@event, MachJson.Options),
            ProcessedUtc = null,
            Attempts = 0,
            Error = null,
        };

        db.OutboxMessages.Add(row);
        return Task.CompletedTask;
    }
}
