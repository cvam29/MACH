using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Azure.Core;
using Azure.Identity;
using Azure.Messaging.ServiceBus;
using Mach.Application.Ports;
using Microsoft.Extensions.Options;

namespace Mach.Infrastructure.Messaging;

/// <summary>
/// Publishes messages to Azure Service Bus topics. One <see cref="ServiceBusSender"/> is
/// created and cached per topic. The bus connects either via a connection string or, when only
/// a namespace is configured, via <see cref="DefaultAzureCredential"/> (managed identity).
/// </summary>
public sealed class ServiceBusMessageBus : IMessageBus, IAsyncDisposable
{
    private readonly ServiceBusClient _client;
    private readonly ConcurrentDictionary<string, ServiceBusSender> _senders = new(StringComparer.Ordinal);
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly bool _ownsClient;
    private volatile bool _disposed;

    /// <summary>Creates the bus from configuration, building the <see cref="ServiceBusClient"/>.</summary>
    public ServiceBusMessageBus(IOptions<MessagingOptions> options)
        : this(CreateClient(options?.Value ?? throw new ArgumentNullException(nameof(options))),
               ownsClient: true,
               jsonOptions: new JsonSerializerOptions(JsonSerializerDefaults.Web))
    {
    }

    /// <summary>
    /// Creates the bus around an existing <see cref="ServiceBusClient"/>. Useful for tests and for
    /// hosts that manage the client lifetime themselves. The client is not disposed by this bus.
    /// </summary>
    public ServiceBusMessageBus(ServiceBusClient client, JsonSerializerOptions? jsonOptions = null)
        : this(client, ownsClient: false, jsonOptions ?? new JsonSerializerOptions(JsonSerializerDefaults.Web))
    {
    }

    private ServiceBusMessageBus(ServiceBusClient client, bool ownsClient, JsonSerializerOptions jsonOptions)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _ownsClient = ownsClient;
        _jsonOptions = jsonOptions;
    }

    private static ServiceBusClient CreateClient(MessagingOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.ConnectionString))
        {
            return new ServiceBusClient(options.ConnectionString);
        }

        if (!string.IsNullOrWhiteSpace(options.Namespace))
        {
            TokenCredential credential = new DefaultAzureCredential();
            return new ServiceBusClient(options.Namespace, credential);
        }

        throw new InvalidOperationException(
            "Messaging:ConnectionString or Messaging:Namespace must be configured for the ServiceBus provider.");
    }

    /// <inheritdoc />
    public async Task PublishAsync<TMessage>(string topic, TMessage message, CancellationToken ct)
        where TMessage : notnull
    {
        ArgumentException.ThrowIfNullOrEmpty(topic);
        ArgumentNullException.ThrowIfNull(message);
        ObjectDisposedException.ThrowIf(_disposed, this);

        var json = JsonSerializer.SerializeToUtf8Bytes(message, _jsonOptions);
        var serviceBusMessage = new ServiceBusMessage(BinaryData.FromBytes(json))
        {
            ContentType = "application/json",
            Subject = typeof(TMessage).FullName ?? typeof(TMessage).Name,
        };

        serviceBusMessage.ApplicationProperties[MessageProperties.MessageType] =
            typeof(TMessage).FullName ?? typeof(TMessage).Name;

        var activity = Activity.Current;
        if (activity is not null)
        {
            var correlationId = activity.TraceId.ToString();
            serviceBusMessage.CorrelationId = correlationId;
            serviceBusMessage.ApplicationProperties[MessageProperties.CorrelationId] = correlationId;
        }

        var sender = GetSender(topic);
        await sender.SendMessageAsync(serviceBusMessage, ct).ConfigureAwait(false);
    }

    /// <summary>Returns the cached sender for <paramref name="topic"/>, creating it on first use.</summary>
    internal ServiceBusSender GetSender(string topic) =>
        _senders.GetOrAdd(topic, static (t, client) => client.CreateSender(t), _client);

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        foreach (var sender in _senders.Values)
        {
            await sender.DisposeAsync().ConfigureAwait(false);
        }

        _senders.Clear();

        if (_ownsClient)
        {
            await _client.DisposeAsync().ConfigureAwait(false);
        }
    }
}
