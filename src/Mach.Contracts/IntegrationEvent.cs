namespace Mach.Contracts;

/// <summary>
/// Base for versioned integration events passed between function hosts over the message bus.
/// All events are serializable records carrying a stable <see cref="EventType"/> and
/// schema <see cref="Version"/> for forward/backward compatibility.
/// </summary>
public abstract record IntegrationEvent
{
    /// <summary>Stable discriminator for routing/deserialization.</summary>
    public abstract string EventType { get; }

    /// <summary>Schema version of this event contract.</summary>
    public int Version { get; init; } = 1;

    /// <summary>Unique id of this event occurrence.</summary>
    public Guid EventId { get; init; } = Guid.NewGuid();

    /// <summary>When the event occurred (UTC).</summary>
    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
}
