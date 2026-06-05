namespace Mach.Persistence.Entities;

/// <summary>
/// An inbound integration event in <c>messaging.InboxEvents</c>, deduplicated by
/// <see cref="DedupKey"/> (unique index).
/// </summary>
public sealed class InboxEventEntity
{
    public Guid Id { get; set; }

    public string Source { get; set; } = default!;

    /// <summary>Dedup key (unique index) — natural idempotency for inbound events.</summary>
    public string DedupKey { get; set; } = default!;

    public DateTimeOffset ReceivedUtc { get; set; }

    public string Status { get; set; } = default!;

    /// <summary>Raw payload (nvarchar(max)).</summary>
    public string RawPayload { get; set; } = default!;
}
