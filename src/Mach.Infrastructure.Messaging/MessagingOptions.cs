namespace Mach.Infrastructure.Messaging;

/// <summary>
/// Selects which <see cref="Mach.Application.Ports.IMessageBus"/> implementation is wired up.
/// </summary>
public enum MessagingProvider
{
    /// <summary>In-process bus backed by channels. No external dependency.</summary>
    InMemory = 0,

    /// <summary>Azure Service Bus topic publisher.</summary>
    ServiceBus = 1,
}

/// <summary>
/// Bound from the <c>Messaging:</c> configuration section.
/// </summary>
public sealed class MessagingOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Messaging";

    /// <summary>Which provider to register. Defaults to <see cref="MessagingProvider.InMemory"/>.</summary>
    public MessagingProvider Provider { get; set; } = MessagingProvider.InMemory;

    /// <summary>
    /// Service Bus connection string. When set, takes precedence over <see cref="Namespace"/>.
    /// Ignored for the in-memory provider.
    /// </summary>
    public string? ConnectionString { get; set; }

    /// <summary>
    /// Fully-qualified Service Bus namespace (e.g. <c>my-ns.servicebus.windows.net</c>),
    /// used with managed identity / <c>DefaultAzureCredential</c> when no connection string is given.
    /// </summary>
    public string? Namespace { get; set; }
}
