namespace Mach.Persistence.Entities;

/// <summary>
/// A transactional notification delivery in <c>notifications.EmailDeliveries</c>.
/// Deduplicated by the unique index on (OrderId, Audience, Kind).
/// </summary>
public sealed class EmailDeliveryEntity
{
    public Guid Id { get; set; }

    public string OrderId { get; set; } = default!;

    public string Audience { get; set; } = default!;

    public string Kind { get; set; } = default!;

    public string? ProviderMessageId { get; set; }

    public string Status { get; set; } = default!;

    public DateTimeOffset SentUtc { get; set; }
}
