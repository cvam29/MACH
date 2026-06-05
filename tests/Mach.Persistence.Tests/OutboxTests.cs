using Mach.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;
using Xunit.Abstractions;

namespace Mach.Persistence.Tests;

[Collection(SqlServerCollection.Name)]
public sealed class OutboxTests(SqlServerFixture fixture, ITestOutputHelper output)
{
    private sealed record SampleEvent(string OrderId, decimal Amount);

    [Fact]
    public async Task Enqueue_then_GetUnsent_returns_oldest_first_and_serializes_payload()
    {
        if (!fixture.SkipIfUnavailable(output))
        {
            return;
        }

        await using var db = fixture.CreateContext();
        var writer = new OutboxWriter(db, TimeProvider.System);

        await writer.EnqueueAsync("orders.placed", new SampleEvent("o-1", 10m), CancellationToken.None);
        await writer.EnqueueAsync("orders.placed", new SampleEvent("o-2", 20m), CancellationToken.None);

        // Writer does NOT save — it joins the caller's unit of work.
        db.ChangeTracker.Entries().Count(e => e.State == EntityState.Added).ShouldBe(2);

        await db.SaveChangesAsync(CancellationToken.None);

        await using var readDb = fixture.CreateContext();
        var reader = new OutboxReader(readDb, TimeProvider.System);
        var batch = await reader.GetUnsentAsync(10, CancellationToken.None);

        batch.Count.ShouldBeGreaterThanOrEqualTo(2);
        var list = batch.ToList();
        var firstIdx = list.FindIndex(m => m.Payload.Contains("o-1"));
        var secondIdx = list.FindIndex(m => m.Payload.Contains("o-2"));
        firstIdx.ShouldBeGreaterThanOrEqualTo(0);
        secondIdx.ShouldBeGreaterThanOrEqualTo(0);
        firstIdx.ShouldBeLessThan(secondIdx); // oldest first
        list[firstIdx].Topic.ShouldBe("orders.placed");
        list[firstIdx].Type.ShouldContain("SampleEvent");
    }

    [Fact]
    public async Task MarkSent_excludes_from_subsequent_GetUnsent()
    {
        if (!fixture.SkipIfUnavailable(output))
        {
            return;
        }

        await using var db = fixture.CreateContext();
        var writer = new OutboxWriter(db, TimeProvider.System);
        await writer.EnqueueAsync("topic.x", new SampleEvent("mark-sent", 1m), CancellationToken.None);
        await db.SaveChangesAsync(CancellationToken.None);

        var reader = new OutboxReader(db, TimeProvider.System);
        var unsent = await reader.GetUnsentAsync(50, CancellationToken.None);
        var target = unsent.Single(m => m.Payload.Contains("mark-sent"));

        await reader.MarkSentAsync(target.Id, CancellationToken.None);

        var after = await reader.GetUnsentAsync(50, CancellationToken.None);
        after.ShouldNotContain(m => m.Id == target.Id);
    }

    [Fact]
    public async Task MarkFailed_increments_attempts_and_stores_error()
    {
        if (!fixture.SkipIfUnavailable(output))
        {
            return;
        }

        await using var db = fixture.CreateContext();
        var writer = new OutboxWriter(db, TimeProvider.System);
        await writer.EnqueueAsync("topic.fail", new SampleEvent("mark-fail", 1m), CancellationToken.None);
        await db.SaveChangesAsync(CancellationToken.None);

        var reader = new OutboxReader(db, TimeProvider.System);
        var target = (await reader.GetUnsentAsync(50, CancellationToken.None))
            .Single(m => m.Payload.Contains("mark-fail"));

        await reader.MarkFailedAsync(target.Id, "boom", CancellationToken.None);
        await reader.MarkFailedAsync(target.Id, "boom-again", CancellationToken.None);

        await using var verifyDb = fixture.CreateContext();
        var row = verifyDb.OutboxMessages.Single(x => x.Id == target.Id);
        row.Attempts.ShouldBe(2);
        row.Error.ShouldBe("boom-again");
        row.ProcessedUtc.ShouldBeNull(); // still unsent after failures
    }
}
