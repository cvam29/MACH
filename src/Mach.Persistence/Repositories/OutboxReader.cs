using Mach.Application.Ports;
using Microsoft.EntityFrameworkCore;

namespace Mach.Persistence.Repositories;

/// <summary>
/// Reads and acknowledges <c>messaging.OutboxMessages</c> rows for the dispatcher.
/// </summary>
internal sealed class OutboxReader(MachDbContext db, TimeProvider time) : IOutboxReader
{
    public async Task<IReadOnlyList<OutboxMessage>> GetUnsentAsync(int batchSize, CancellationToken ct)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(batchSize);

        return await db.OutboxMessages
            .Where(x => x.ProcessedUtc == null)
            .OrderBy(x => x.OccurredUtc)
            .ThenBy(x => x.Id)
            .Take(batchSize)
            .Select(x => new OutboxMessage(x.Id, x.Topic, x.Type, x.Payload))
            .ToListAsync(ct);
    }

    public async Task MarkSentAsync(Guid id, CancellationToken ct)
    {
        var row = await db.OutboxMessages.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (row is null)
        {
            return;
        }

        row.ProcessedUtc = time.GetUtcNow();
        row.Error = null;
        await db.SaveChangesAsync(ct);
    }

    public async Task MarkFailedAsync(Guid id, string error, CancellationToken ct)
    {
        var row = await db.OutboxMessages.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (row is null)
        {
            return;
        }

        row.Attempts += 1;
        row.Error = error;
        await db.SaveChangesAsync(ct);
    }
}
