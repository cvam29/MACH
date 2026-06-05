using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Mach.Application.Ports;

namespace Mach.Infrastructure.Messaging;

/// <summary>
/// An in-process <see cref="IMessageBus"/> used for local hosting and tests, where no
/// Azure Service Bus is available. Publishers and subscribers must share the SAME instance
/// (registered as a singleton by <c>AddMessaging</c>) for messages to flow.
/// </summary>
/// <remarks>
/// <para>
/// Wave-2 hosts subscribe via <see cref="Subscribe{TMessage}(string, Func{ReceivedMessage{TMessage}, CancellationToken, Task})"/>
/// (or the synchronous overload). Each subscription gets its own unbounded channel and a
/// background pump task, so a slow handler does not block the publisher or sibling subscribers.
/// </para>
/// <para>
/// Delivery is fan-out: every subscriber to a topic receives every message published to that
/// topic. Bodies are round-tripped through System.Text.Json so subscribers observe the same
/// serialization semantics as the Service Bus bus. The class is thread-safe.
/// </para>
/// </remarks>
public sealed class InMemoryMessageBus : IMessageBus, IAsyncDisposable
{
    private readonly ConcurrentDictionary<string, Topic> _topics = new(StringComparer.Ordinal);
    private readonly JsonSerializerOptions _jsonOptions;
    private volatile bool _disposed;

    /// <summary>Creates a bus using default JSON serialization.</summary>
    public InMemoryMessageBus()
        : this(new JsonSerializerOptions(JsonSerializerDefaults.Web))
    {
    }

    /// <summary>Creates a bus with custom JSON serialization options (e.g. to match the Service Bus bus).</summary>
    public InMemoryMessageBus(JsonSerializerOptions jsonOptions)
    {
        _jsonOptions = jsonOptions ?? throw new ArgumentNullException(nameof(jsonOptions));
    }

    /// <inheritdoc />
    public async Task PublishAsync<TMessage>(string topic, TMessage message, CancellationToken ct)
        where TMessage : notnull
    {
        ArgumentException.ThrowIfNullOrEmpty(topic);
        ArgumentNullException.ThrowIfNull(message);
        ObjectDisposedException.ThrowIf(_disposed, this);

        var properties = BuildProperties(typeof(TMessage));

        // Round-trip through JSON so subscribers see the serialized shape (and cannot mutate
        // the publisher's instance). Payload carries the raw JSON; subscribers deserialize.
        var json = JsonSerializer.Serialize(message, _jsonOptions);
        var envelope = new Envelope(json, properties);

        if (!_topics.TryGetValue(topic, out var entry))
        {
            // No subscribers for this topic yet: nothing to deliver to.
            return;
        }

        await entry.PublishAsync(envelope, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Registers an asynchronous handler that receives every message published to
    /// <paramref name="topic"/>. Returns an <see cref="IDisposable"/> that unsubscribes.
    /// </summary>
    /// <typeparam name="TMessage">The message type to deserialize bodies into.</typeparam>
    /// <param name="topic">Topic name to subscribe to.</param>
    /// <param name="handler">Invoked once per delivered message.</param>
    public IDisposable Subscribe<TMessage>(
        string topic,
        Func<ReceivedMessage<TMessage>, CancellationToken, Task> handler)
        where TMessage : notnull
    {
        ArgumentException.ThrowIfNullOrEmpty(topic);
        ArgumentNullException.ThrowIfNull(handler);
        ObjectDisposedException.ThrowIf(_disposed, this);

        var entry = _topics.GetOrAdd(topic, static _ => new Topic());
        return entry.AddSubscriber(envelope => DispatchAsync(envelope, handler));
    }

    /// <summary>Synchronous-handler convenience overload of
    /// <see cref="Subscribe{TMessage}(string, Func{ReceivedMessage{TMessage}, CancellationToken, Task})"/>.</summary>
    public IDisposable Subscribe<TMessage>(string topic, Action<ReceivedMessage<TMessage>> handler)
        where TMessage : notnull
    {
        ArgumentNullException.ThrowIfNull(handler);
        return Subscribe<TMessage>(topic, (msg, _) =>
        {
            handler(msg);
            return Task.CompletedTask;
        });
    }

    private async Task DispatchAsync<TMessage>(
        Envelope envelope,
        Func<ReceivedMessage<TMessage>, CancellationToken, Task> handler)
        where TMessage : notnull
    {
        var body = JsonSerializer.Deserialize<TMessage>(envelope.Json, _jsonOptions)
            ?? throw new InvalidOperationException(
                $"In-memory message for type '{typeof(TMessage)}' deserialized to null.");

        var received = new ReceivedMessage<TMessage>(body, envelope.Properties);
        await handler(received, CancellationToken.None).ConfigureAwait(false);
    }

    private static Dictionary<string, string> BuildProperties(Type messageType)
    {
        var properties = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [MessageProperties.MessageType] = messageType.FullName ?? messageType.Name,
        };

        var activity = Activity.Current;
        if (activity is not null)
        {
            properties[MessageProperties.CorrelationId] = activity.TraceId.ToString();
        }

        return properties;
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        foreach (var topic in _topics.Values)
        {
            await topic.DisposeAsync().ConfigureAwait(false);
        }

        _topics.Clear();
    }

    private readonly record struct Envelope(string Json, IReadOnlyDictionary<string, string> Properties);

    /// <summary>Per-topic registry of subscriber pumps. Thread-safe.</summary>
    private sealed class Topic : IAsyncDisposable
    {
        private readonly ConcurrentDictionary<Guid, Subscription> _subscribers = new();

        public IDisposable AddSubscriber(Func<Envelope, Task> dispatch)
        {
            var id = Guid.NewGuid();
            var subscription = new Subscription(dispatch, () => _subscribers.TryRemove(id, out _));
            _subscribers[id] = subscription;
            return subscription;
        }

        public async Task PublishAsync(Envelope envelope, CancellationToken ct)
        {
            // Snapshot avoids holding any lock while writing to channels.
            foreach (var subscription in _subscribers.Values)
            {
                await subscription.EnqueueAsync(envelope, ct).ConfigureAwait(false);
            }
        }

        public async ValueTask DisposeAsync()
        {
            foreach (var subscription in _subscribers.Values)
            {
                await subscription.DisposeAsyncCore().ConfigureAwait(false);
            }

            _subscribers.Clear();
        }
    }

    /// <summary>A single subscription: an unbounded channel plus a background pump task.</summary>
    private sealed class Subscription : IDisposable
    {
        private readonly System.Threading.Channels.Channel<Envelope> _channel;
        private readonly Func<Envelope, Task> _dispatch;
        private readonly Action _onDispose;
        private readonly Task _pump;
        private int _disposed;

        public Subscription(Func<Envelope, Task> dispatch, Action onDispose)
        {
            _dispatch = dispatch;
            _onDispose = onDispose;
            _channel = System.Threading.Channels.Channel.CreateUnbounded<Envelope>(
                new System.Threading.Channels.UnboundedChannelOptions
                {
                    SingleReader = true,
                    SingleWriter = false,
                });
            _pump = Task.Run(PumpAsync);
        }

        public async Task EnqueueAsync(Envelope envelope, CancellationToken ct)
        {
            if (Volatile.Read(ref _disposed) != 0)
            {
                return;
            }

            await _channel.Writer.WriteAsync(envelope, ct).ConfigureAwait(false);
        }

        private async Task PumpAsync()
        {
            await foreach (var envelope in _channel.Reader.ReadAllAsync().ConfigureAwait(false))
            {
                await _dispatch(envelope).ConfigureAwait(false);
            }
        }

        public void Dispose()
        {
            // Fire-and-forget the async drain; callers of Subscribe expect a sync IDisposable.
            _ = DisposeAsyncCore();
        }

        public async ValueTask DisposeAsyncCore()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
            {
                return;
            }

            _onDispose();
            _channel.Writer.TryComplete();
            try
            {
                await _pump.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // pump cancelled during shutdown — ignore.
            }
        }
    }
}
