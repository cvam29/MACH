using Mach.Application.Ports;
using Mach.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace Mach.Persistence.Repositories;

/// <summary>
/// Idempotency store backed by <c>idempotency.IdempotencyKeys</c>. <see cref="TryBeginAsync"/>
/// races via an atomic insert: the winner gets <see cref="IdempotencyState.Began"/>; a
/// primary-key violation means another caller already claimed the key, so the existing
/// state is returned.
/// </summary>
internal sealed class IdempotencyStore(MachDbContext db) : IIdempotencyStore
{
    private static readonly TimeSpan DefaultTtl = TimeSpan.FromHours(24);

    public async Task<IdempotencyState> TryBeginAsync(string key, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        var now = DateTimeOffset.UtcNow;
        var entity = new IdempotencyKeyEntity
        {
            Key = key,
            RequestHash = string.Empty,
            ResponsePayload = null,
            State = nameof(IdempotencyState.InProgress),
            CreatedUtc = now,
            ExpiresUtc = now.Add(DefaultTtl),
        };

        db.IdempotencyKeys.Add(entity);

        try
        {
            await db.SaveChangesAsync(ct);
            return IdempotencyState.Began;
        }
        catch (DbUpdateException)
        {
            // Likely a duplicate-key violation — another caller already claimed it.
            // Detach our failed insert and report the existing state. If no row exists,
            // the failure was something else, so re-throw.
            db.Entry(entity).State = EntityState.Detached;

            var existing = await GetExistingAsync(key, ct);
            if (existing is null)
            {
                throw;
            }

            return existing.State;
        }
    }

    public async Task<IdempotencyRecord?> GetExistingAsync(string key, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        var row = await db.IdempotencyKeys
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Key == key, ct);

        if (row is null)
        {
            return null;
        }

        var state = Enum.TryParse<IdempotencyState>(row.State, out var parsed)
            ? parsed
            : IdempotencyState.InProgress;

        return new IdempotencyRecord(row.Key, state, row.ResponsePayload);
    }

    public async Task CompleteWithAsync(string key, string responsePayload, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        var row = await db.IdempotencyKeys.FirstOrDefaultAsync(x => x.Key == key, ct);
        if (row is null)
        {
            return;
        }

        row.State = nameof(IdempotencyState.Completed);
        row.ResponsePayload = responsePayload;
        await db.SaveChangesAsync(ct);
    }
}
