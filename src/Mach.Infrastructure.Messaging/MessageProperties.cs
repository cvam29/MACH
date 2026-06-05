using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Mach.Infrastructure.Messaging.Tests")]

namespace Mach.Infrastructure.Messaging;

/// <summary>
/// Well-known <c>ApplicationProperties</c> keys attached to every published message,
/// shared by both the Service Bus and in-memory buses so consumers read them uniformly.
/// </summary>
public static class MessageProperties
{
    /// <summary>CLR type name of the serialized message body.</summary>
    public const string MessageType = "MessageType";

    /// <summary>Distributed-trace correlation id (W3C trace id when an <see cref="System.Diagnostics.Activity"/> is current).</summary>
    public const string CorrelationId = "CorrelationId";
}
