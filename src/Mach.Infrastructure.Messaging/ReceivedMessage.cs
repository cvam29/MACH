using System.Collections.Generic;

namespace Mach.Infrastructure.Messaging;

/// <summary>
/// A message delivered to an in-memory subscriber: the deserialized body plus the
/// application properties (message type, correlation id, ...) that were set at publish time.
/// </summary>
/// <typeparam name="TMessage">The published message type.</typeparam>
public sealed record ReceivedMessage<TMessage>(
    TMessage Body,
    IReadOnlyDictionary<string, string> Properties)
    where TMessage : notnull
{
    /// <summary>The CLR type name of the body, as recorded at publish time (may be null if absent).</summary>
    public string? MessageType =>
        Properties.TryGetValue(MessageProperties.MessageType, out var v) ? v : null;

    /// <summary>The correlation id captured from the ambient <see cref="System.Diagnostics.Activity"/> at publish time, if any.</summary>
    public string? CorrelationId =>
        Properties.TryGetValue(MessageProperties.CorrelationId, out var v) ? v : null;
}
