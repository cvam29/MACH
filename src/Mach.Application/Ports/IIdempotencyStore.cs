using Mach.Domain;

namespace Mach.Application.Ports;

/// <summary>The state of an idempotency key.</summary>
public enum IdempotencyState
{
    /// <summary>The caller acquired the key and should perform the work.</summary>
    Began,

    /// <summary>The key already exists and is still in flight.</summary>
    InProgress,

    /// <summary>The key already exists and a result was recorded.</summary>
    Completed,
}

/// <summary>An existing idempotency record, with the stored response payload if completed.</summary>
public sealed record IdempotencyRecord(string Key, IdempotencyState State, string? ResponsePayload);

/// <summary>
/// Honors client <c>Idempotency-Key</c> headers / webhook dedup keys.
/// Implemented by <c>Mach.Persistence</c>.
/// </summary>
public interface IIdempotencyStore
{
    /// <summary>
    /// Atomically claim <paramref name="key"/>. Returns <see cref="IdempotencyState.Began"/>
    /// when the caller won the race and should do the work; otherwise the existing state.
    /// </summary>
    Task<IdempotencyState> TryBeginAsync(string key, CancellationToken ct);

    Task<IdempotencyRecord?> GetExistingAsync(string key, CancellationToken ct);

    Task CompleteWithAsync(string key, string responsePayload, CancellationToken ct);
}
