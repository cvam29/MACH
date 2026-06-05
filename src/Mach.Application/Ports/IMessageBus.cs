namespace Mach.Application.Ports;

/// <summary>
/// Port over the message bus (Azure Service Bus, or an in-memory fallback).
/// Implemented by <c>Mach.Infrastructure.Messaging</c>.
/// </summary>
public interface IMessageBus
{
    Task PublishAsync<TMessage>(string topic, TMessage message, CancellationToken ct)
        where TMessage : notnull;
}
