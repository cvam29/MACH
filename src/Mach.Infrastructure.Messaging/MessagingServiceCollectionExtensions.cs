using System;
using Mach.Application.Ports;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Mach.Infrastructure.Messaging;

/// <summary>
/// Registers the messaging adapter (<see cref="IMessageBus"/>) selected by configuration.
/// </summary>
public static class MessagingServiceCollectionExtensions
{
    /// <summary>
    /// Binds the <c>Messaging:</c> section and registers the chosen <see cref="IMessageBus"/>.
    /// <list type="bullet">
    /// <item><see cref="MessagingProvider.InMemory"/> registers a single shared
    /// <see cref="InMemoryMessageBus"/> as both the concrete type and <see cref="IMessageBus"/>,
    /// so publishers and subscribers share one instance.</item>
    /// <item><see cref="MessagingProvider.ServiceBus"/> registers a singleton
    /// <see cref="ServiceBusMessageBus"/>.</item>
    /// </list>
    /// </summary>
    public static IServiceCollection AddMessaging(this IServiceCollection services, IConfiguration config)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(config);

        var section = config.GetSection(MessagingOptions.SectionName);
        services.AddOptions<MessagingOptions>().Bind(section);

        // Read provider eagerly so we register the right implementation.
        var options = section.Get<MessagingOptions>() ?? new MessagingOptions();

        switch (options.Provider)
        {
            case MessagingProvider.ServiceBus:
                services.TryAddSingleton<ServiceBusMessageBus>();
                services.TryAddSingleton<IMessageBus>(sp => sp.GetRequiredService<ServiceBusMessageBus>());
                break;

            case MessagingProvider.InMemory:
            default:
                // Single shared instance: subscribers (resolving InMemoryMessageBus) and
                // publishers (resolving IMessageBus) must see the same object.
                services.TryAddSingleton<InMemoryMessageBus>();
                services.TryAddSingleton<IMessageBus>(sp => sp.GetRequiredService<InMemoryMessageBus>());
                break;
        }

        return services;
    }
}
