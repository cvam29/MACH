namespace Mach.Persistence.Entities;

/// <summary>
/// A transactional-outbox row in <c>messaging.OutboxMessages</c>. Written inside the
/// caller's unit of work and published after the surrounding transaction commits.
/// </summary>
public sealed class OutboxMessageEntity
{
    public Guid Id { get; set; }

    public DateTimeOffset OccurredUtc { get; set; }

    public string Topic { get; set; } = default!;

    public string Type { get; set; } = default!;

    /// <summary>JSON payload (nvarchar(max)).</summary>
    public string Payload { get; set; } = default!;

    public DateTimeOffset? ProcessedUtc { get; set; }

    public int Attempts { get; set; }

    public string? Error { get; set; }
}
