using Mach.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;
using Xunit.Abstractions;

namespace Mach.Persistence.Tests;

[Collection(SqlServerCollection.Name)]
public sealed class UniqueConstraintTests(SqlServerFixture fixture, ITestOutputHelper output)
{
    [Fact]
    public async Task EmailDeliveries_unique_on_OrderId_Audience_Kind()
    {
        if (!fixture.SkipIfUnavailable(output))
        {
            return;
        }

        var orderId = $"ord-{Guid.NewGuid():N}";

        await using var db = fixture.CreateContext();
        db.EmailDeliveries.Add(new EmailDeliveryEntity
        {
            Id = Guid.NewGuid(),
            OrderId = orderId,
            Audience = "Customer",
            Kind = "OrderConfirmation",
            Status = "Sent",
            SentUtc = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync(CancellationToken.None);

        // Same (OrderId, Audience, Kind) must violate the unique index.
        await using var db2 = fixture.CreateContext();
        db2.EmailDeliveries.Add(new EmailDeliveryEntity
        {
            Id = Guid.NewGuid(),
            OrderId = orderId,
            Audience = "Customer",
            Kind = "OrderConfirmation",
            Status = "Sent",
            SentUtc = DateTimeOffset.UtcNow,
        });

        await Should.ThrowAsync<DbUpdateException>(() => db2.SaveChangesAsync(CancellationToken.None));

        // A different audience for the same order/kind is allowed.
        await using var db3 = fixture.CreateContext();
        db3.EmailDeliveries.Add(new EmailDeliveryEntity
        {
            Id = Guid.NewGuid(),
            OrderId = orderId,
            Audience = "Store",
            Kind = "OrderConfirmation",
            Status = "Sent",
            SentUtc = DateTimeOffset.UtcNow,
        });
        await Should.NotThrowAsync(() => db3.SaveChangesAsync(CancellationToken.None));
    }

    [Fact]
    public async Task InboxEvents_unique_on_DedupKey()
    {
        if (!fixture.SkipIfUnavailable(output))
        {
            return;
        }

        var dedupKey = $"dedup-{Guid.NewGuid():N}";

        await using var db = fixture.CreateContext();
        db.InboxEvents.Add(new InboxEventEntity
        {
            Id = Guid.NewGuid(),
            Source = "adyen",
            DedupKey = dedupKey,
            ReceivedUtc = DateTimeOffset.UtcNow,
            Status = "Received",
            RawPayload = "{}",
        });
        await db.SaveChangesAsync(CancellationToken.None);

        await using var db2 = fixture.CreateContext();
        db2.InboxEvents.Add(new InboxEventEntity
        {
            Id = Guid.NewGuid(),
            Source = "adyen",
            DedupKey = dedupKey,
            ReceivedUtc = DateTimeOffset.UtcNow,
            Status = "Received",
            RawPayload = "{}",
        });

        await Should.ThrowAsync<DbUpdateException>(() => db2.SaveChangesAsync(CancellationToken.None));
    }
}
