using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Mach.Application.Ports;
using Mach.Infrastructure.Messaging;
using Shouldly;

namespace Mach.Infrastructure.Messaging.Tests;

public sealed class InMemoryMessageBusTests
{
    private sealed record OrderPlaced(Guid OrderId, decimal Total);

    private sealed record StockReserved(string Sku, int Quantity);

    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    [Fact]
    public async Task Publish_DeliversToSubscribedHandler()
    {
        await using var bus = new InMemoryMessageBus();
        var tcs = new TaskCompletionSource<ReceivedMessage<OrderPlaced>>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        using var _ = bus.Subscribe<OrderPlaced>("orders", msg => tcs.TrySetResult(msg));

        var id = Guid.NewGuid();
        await bus.PublishAsync("orders", new OrderPlaced(id, 42.5m), default);

        var received = await tcs.Task.WaitAsync(Timeout);
        received.Body.OrderId.ShouldBe(id);
        received.Body.Total.ShouldBe(42.5m);
    }

    [Fact]
    public async Task Publish_BeforeAnySubscriber_IsDroppedNotThrown()
    {
        await using var bus = new InMemoryMessageBus();

        // No subscriber registered: should be a no-op, not an exception.
        await Should.NotThrowAsync(async () =>
            await bus.PublishAsync("orders", new OrderPlaced(Guid.NewGuid(), 1m), default));
    }

    [Fact]
    public async Task Publish_FansOutToMultipleSubscribers()
    {
        await using var bus = new InMemoryMessageBus();
        var received = new ConcurrentBag<Guid>();
        var countdown = new CountdownEvent(3);

        IDisposable Sub() => bus.Subscribe<OrderPlaced>("orders", msg =>
        {
            received.Add(msg.Body.OrderId);
            countdown.Signal();
        });

        using var s1 = Sub();
        using var s2 = Sub();
        using var s3 = Sub();

        var id = Guid.NewGuid();
        await bus.PublishAsync("orders", new OrderPlaced(id, 1m), default);

        countdown.Wait(Timeout).ShouldBeTrue();
        received.Count.ShouldBe(3);
        received.ShouldAllBe(x => x == id);
    }

    [Fact]
    public async Task Topics_AreIsolated()
    {
        await using var bus = new InMemoryMessageBus();
        var orderTcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var stockHits = 0;

        using var _ = bus.Subscribe<OrderPlaced>("orders", _ => orderTcs.TrySetResult());
        using var __ = bus.Subscribe<StockReserved>("stock", _ => Interlocked.Increment(ref stockHits));

        await bus.PublishAsync("orders", new OrderPlaced(Guid.NewGuid(), 1m), default);

        await orderTcs.Task.WaitAsync(Timeout);

        // Give any (erroneous) cross-topic delivery a chance to land.
        await Task.Delay(50);
        Volatile.Read(ref stockHits).ShouldBe(0);
    }

    [Fact]
    public async Task MessageType_PropertyIsPropagated()
    {
        await using var bus = new InMemoryMessageBus();
        var tcs = new TaskCompletionSource<ReceivedMessage<OrderPlaced>>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        using var _ = bus.Subscribe<OrderPlaced>("orders", msg => tcs.TrySetResult(msg));

        await bus.PublishAsync("orders", new OrderPlaced(Guid.NewGuid(), 1m), default);

        var received = await tcs.Task.WaitAsync(Timeout);
        received.MessageType.ShouldBe(typeof(OrderPlaced).FullName);
        received.Properties.ShouldContainKey(MessageProperties.MessageType);
    }

    [Fact]
    public async Task CorrelationId_FromActivity_IsPropagated()
    {
        await using var bus = new InMemoryMessageBus();
        var tcs = new TaskCompletionSource<ReceivedMessage<OrderPlaced>>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        using var _ = bus.Subscribe<OrderPlaced>("orders", msg => tcs.TrySetResult(msg));

        // Activity needs a listener that samples, otherwise Activity.Current stays null.
        using var listener = new ActivityListener
        {
            ShouldListenTo = _ => true,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
        };
        ActivitySource.AddActivityListener(listener);
        using var source = new ActivitySource("Mach.Tests");

        string expectedTraceId;
        using (var activity = source.StartActivity("publish"))
        {
            activity.ShouldNotBeNull();
            expectedTraceId = activity!.TraceId.ToString();
            await bus.PublishAsync("orders", new OrderPlaced(Guid.NewGuid(), 1m), default);
        }

        var received = await tcs.Task.WaitAsync(Timeout);
        received.CorrelationId.ShouldBe(expectedTraceId);
    }

    [Fact]
    public async Task NoActivity_OmitsCorrelationId()
    {
        // Ensure no ambient activity is flowing.
        Activity.Current = null;

        await using var bus = new InMemoryMessageBus();
        var tcs = new TaskCompletionSource<ReceivedMessage<OrderPlaced>>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        using var _ = bus.Subscribe<OrderPlaced>("orders", msg => tcs.TrySetResult(msg));
        await bus.PublishAsync("orders", new OrderPlaced(Guid.NewGuid(), 1m), default);

        var received = await tcs.Task.WaitAsync(Timeout);
        received.CorrelationId.ShouldBeNull();
        received.Properties.ShouldNotContainKey(MessageProperties.CorrelationId);
    }

    [Fact]
    public async Task Unsubscribe_StopsDelivery()
    {
        await using var bus = new InMemoryMessageBus();
        var hits = 0;

        var subscription = bus.Subscribe<OrderPlaced>("orders", _ => Interlocked.Increment(ref hits));
        subscription.Dispose();

        await bus.PublishAsync("orders", new OrderPlaced(Guid.NewGuid(), 1m), default);
        await Task.Delay(50);

        Volatile.Read(ref hits).ShouldBe(0);
    }

    [Fact]
    public async Task Concurrency_AllMessagesDelivered()
    {
        await using var bus = new InMemoryMessageBus();
        const int publishers = 8;
        const int perPublisher = 250;
        const int total = publishers * perPublisher;

        var countdown = new CountdownEvent(total);
        using var _ = bus.Subscribe<OrderPlaced>("orders", _ => countdown.Signal());

        var tasks = Enumerable.Range(0, publishers).Select(_ => Task.Run(async () =>
        {
            for (var i = 0; i < perPublisher; i++)
            {
                await bus.PublishAsync("orders", new OrderPlaced(Guid.NewGuid(), i), default);
            }
        }));

        await Task.WhenAll(tasks);

        countdown.Wait(TimeSpan.FromSeconds(15)).ShouldBeTrue(
            $"only {total - countdown.CurrentCount} of {total} messages were delivered");
    }

    [Fact]
    public async Task Publish_RoundTripsThroughJson_SubscriberSeesCopy()
    {
        await using var bus = new InMemoryMessageBus();
        var tcs = new TaskCompletionSource<ReceivedMessage<OrderPlaced>>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        using var _ = bus.Subscribe<OrderPlaced>("orders", msg => tcs.TrySetResult(msg));

        var original = new OrderPlaced(Guid.NewGuid(), 99.99m);
        await bus.PublishAsync("orders", original, default);

        var received = await tcs.Task.WaitAsync(Timeout);
        received.Body.ShouldBe(original);            // value-equal
        ReferenceEquals(received.Body, original).ShouldBeFalse(); // but a distinct instance
    }

    [Fact]
    public async Task Publish_AfterDispose_Throws()
    {
        var bus = new InMemoryMessageBus();
        await bus.DisposeAsync();

        await Should.ThrowAsync<ObjectDisposedException>(async () =>
            await bus.PublishAsync("orders", new OrderPlaced(Guid.NewGuid(), 1m), default));
    }

    [Fact]
    public async Task Publish_NullOrEmptyTopic_Throws()
    {
        await using var bus = new InMemoryMessageBus();
        await Should.ThrowAsync<ArgumentException>(async () =>
            await bus.PublishAsync("", new OrderPlaced(Guid.NewGuid(), 1m), default));
    }
}
