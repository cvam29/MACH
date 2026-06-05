namespace Mach.Persistence.Entities;

/// <summary>
/// An audited inbound webhook delivery in <c>audit.WebhookDeliveries</c>.
/// </summary>
public sealed class WebhookDeliveryEntity
{
    public Guid Id { get; set; }

    public string Source { get; set; } = default!;

    public DateTimeOffset ReceivedUtc { get; set; }

    public string Status { get; set; } = default!;

    public int LatencyMs { get; set; }

    public bool SignatureValid { get; set; }

    public string? Notes { get; set; }
}
