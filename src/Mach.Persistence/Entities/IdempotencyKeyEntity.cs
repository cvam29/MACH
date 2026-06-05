namespace Mach.Persistence.Entities;

/// <summary>
/// An idempotency record in <c>idempotency.IdempotencyKeys</c>, keyed by the client
/// <c>Idempotency-Key</c> header or a webhook dedup key.
/// </summary>
public sealed class IdempotencyKeyEntity
{
    public string Key { get; set; } = default!;

    public string RequestHash { get; set; } = default!;

    /// <summary>Stored response payload once the work has completed.</summary>
    public string? ResponsePayload { get; set; }

    public string State { get; set; } = default!;

    public DateTimeOffset CreatedUtc { get; set; }

    public DateTimeOffset ExpiresUtc { get; set; }
}
