using Mach.Application.Ports;
using Mach.Outbox.Functions;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;

namespace Mach.Outbox.Functions.Tests;

public sealed class OutboxDispatcherTests
{
    [Fact]
    public async Task Publishes_and_marks_sent_in_order()
    {
        var m1 = new OutboxMessage(Guid.NewGuid(), "orders", "OrderPlaced", "p1");
        var m2 = new OutboxMessage(Guid.NewGuid(), "orders", "OrderShipped", "p2");
        var m3 = new OutboxMessage(Guid.NewGuid(), "payments", "PaymentCaptured", "p3");

        var reader = new FakeOutboxReader(m1, m2, m3);
        var bus = new FakeMessageBus();
        var sut = new OutboxDispatcher(reader, bus, NullLogger<OutboxDispatcher>.Instance);

        var sent = await sut.DispatchPendingAsync(CancellationToken.None);

        sent.ShouldBe(3);

        // Published to the right topics with the raw payload, oldest-first.
        bus.Published.ShouldBe(
        [
            ("orders", "p1"),
            ("orders", "p2"),
            ("payments", "p3"),
        ]);

        // All acknowledged, in order; none failed.
        reader.SentIds.ShouldBe([m1.Id, m2.Id, m3.Id]);
        reader.FailedIds.ShouldBeEmpty();
    }

    [Fact]
    public async Task A_publish_failure_marks_that_one_failed_and_others_still_process()
    {
        var m1 = new OutboxMessage(Guid.NewGuid(), "orders", "OrderPlaced", "ok-1");
        var poison = new OutboxMessage(Guid.NewGuid(), "orders", "Bad", "boom");
        var m3 = new OutboxMessage(Guid.NewGuid(), "orders", "OrderShipped", "ok-3");

        var reader = new FakeOutboxReader(m1, poison, m3);
        var bus = new FakeMessageBus(failOnPayload: "boom");
        var sut = new OutboxDispatcher(reader, bus, NullLogger<OutboxDispatcher>.Instance);

        var sent = await sut.DispatchPendingAsync(CancellationToken.None);

        sent.ShouldBe(2);

        // The two healthy messages were published; the poison one was attempted but not acked.
        reader.SentIds.ShouldBe([m1.Id, m3.Id]);

        // The poison message is recorded as failed with the publish error.
        reader.FailedIds.ShouldBe([poison.Id]);
        reader.FailedErrors.Single().ShouldContain("boom");
    }

    [Fact]
    public async Task Empty_batch_publishes_nothing()
    {
        var reader = new FakeOutboxReader();
        var bus = new FakeMessageBus();
        var sut = new OutboxDispatcher(reader, bus, NullLogger<OutboxDispatcher>.Instance);

        var sent = await sut.DispatchPendingAsync(CancellationToken.None);

        sent.ShouldBe(0);
        bus.Published.ShouldBeEmpty();
        reader.SentIds.ShouldBeEmpty();
    }

    private sealed class FakeOutboxReader : IOutboxReader
    {
        private readonly List<OutboxMessage> _unsent;

        public FakeOutboxReader(params OutboxMessage[] unsent) => _unsent = unsent.ToList();

        public List<Guid> SentIds { get; } = [];
        public List<Guid> FailedIds { get; } = [];
        public List<string> FailedErrors { get; } = [];

        public Task<IReadOnlyList<OutboxMessage>> GetUnsentAsync(int batchSize, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<OutboxMessage>>(_unsent.Take(batchSize).ToList());

        public Task MarkSentAsync(Guid id, CancellationToken ct)
        {
            SentIds.Add(id);
            return Task.CompletedTask;
        }

        public Task MarkFailedAsync(Guid id, string error, CancellationToken ct)
        {
            FailedIds.Add(id);
            FailedErrors.Add(error);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeMessageBus : IMessageBus
    {
        private readonly string? _failOnPayload;

        public FakeMessageBus(string? failOnPayload = null) => _failOnPayload = failOnPayload;

        public List<(string Topic, string Payload)> Published { get; } = [];

        public Task PublishAsync<TMessage>(string topic, TMessage message, CancellationToken ct)
            where TMessage : notnull
        {
            var payload = message.ToString() ?? string.Empty;
            if (_failOnPayload is not null && payload == _failOnPayload)
            {
                throw new InvalidOperationException($"publish failed for payload '{payload}'");
            }

            Published.Add((topic, payload));
            return Task.CompletedTask;
        }
    }
}
