using Mach.Application.Ports;
using Mach.Persistence.Repositories;
using Shouldly;
using Xunit;
using Xunit.Abstractions;

namespace Mach.Persistence.Tests;

[Collection(SqlServerCollection.Name)]
public sealed class IdempotencyTests(SqlServerFixture fixture, ITestOutputHelper output)
{
    [Fact]
    public async Task TryBegin_first_caller_wins_duplicate_returns_existing_state()
    {
        if (!fixture.SkipIfUnavailable(output))
        {
            return;
        }

        var key = $"idem-{Guid.NewGuid():N}";

        await using var db1 = fixture.CreateContext();
        var store1 = new IdempotencyStore(db1, TimeProvider.System);
        var first = await store1.TryBeginAsync(key, CancellationToken.None);
        first.ShouldBe(IdempotencyState.Began);

        // Second caller on a fresh context loses the race.
        await using var db2 = fixture.CreateContext();
        var store2 = new IdempotencyStore(db2, TimeProvider.System);
        var second = await store2.TryBeginAsync(key, CancellationToken.None);
        second.ShouldBe(IdempotencyState.InProgress);
    }

    [Fact]
    public async Task GetExisting_returns_null_when_absent()
    {
        if (!fixture.SkipIfUnavailable(output))
        {
            return;
        }

        await using var db = fixture.CreateContext();
        var store = new IdempotencyStore(db, TimeProvider.System);

        var record = await store.GetExistingAsync($"missing-{Guid.NewGuid():N}", CancellationToken.None);
        record.ShouldBeNull();
    }

    [Fact]
    public async Task CompleteWith_records_response_and_completed_state()
    {
        if (!fixture.SkipIfUnavailable(output))
        {
            return;
        }

        var key = $"idem-{Guid.NewGuid():N}";

        await using var db = fixture.CreateContext();
        var store = new IdempotencyStore(db, TimeProvider.System);

        (await store.TryBeginAsync(key, CancellationToken.None)).ShouldBe(IdempotencyState.Began);

        await store.CompleteWithAsync(key, "{\"ok\":true}", CancellationToken.None);

        await using var verifyDb = fixture.CreateContext();
        var verifyStore = new IdempotencyStore(verifyDb, TimeProvider.System);
        var record = await verifyStore.GetExistingAsync(key, CancellationToken.None);

        record.ShouldNotBeNull();
        record!.State.ShouldBe(IdempotencyState.Completed);
        record.ResponsePayload.ShouldBe("{\"ok\":true}");

        // A later TryBegin must observe the completed state, not re-Begin.
        await using var dbLate = fixture.CreateContext();
        var storeLate = new IdempotencyStore(dbLate, TimeProvider.System);
        (await storeLate.TryBeginAsync(key, CancellationToken.None)).ShouldBe(IdempotencyState.Completed);
    }
}
