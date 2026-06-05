using System.Collections.Generic;
using Mach.Application.Ports;
using Mach.Infrastructure.Messaging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace Mach.Infrastructure.Messaging.Tests;

public sealed class AddMessagingTests
{
    private static IConfiguration Config(Dictionary<string, string?> values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values).Build();

    [Fact]
    public void InMemory_Provider_RegistersInMemoryBus()
    {
        var config = Config(new() { ["Messaging:Provider"] = "InMemory" });

        var provider = new ServiceCollection().AddMessaging(config).BuildServiceProvider();

        provider.GetRequiredService<IMessageBus>().ShouldBeOfType<InMemoryMessageBus>();
    }

    [Fact]
    public void InMemory_PublisherAndSubscriber_ShareSameInstance()
    {
        var config = Config(new() { ["Messaging:Provider"] = "InMemory" });
        var provider = new ServiceCollection().AddMessaging(config).BuildServiceProvider();

        var asBus = provider.GetRequiredService<IMessageBus>();
        var asConcrete = provider.GetRequiredService<InMemoryMessageBus>();

        ReferenceEquals(asBus, asConcrete).ShouldBeTrue();
    }

    [Fact]
    public void DefaultProvider_WhenUnset_IsInMemory()
    {
        var provider = new ServiceCollection()
            .AddMessaging(Config(new()))
            .BuildServiceProvider();

        provider.GetRequiredService<IMessageBus>().ShouldBeOfType<InMemoryMessageBus>();
    }

    [Fact]
    public void ServiceBus_Provider_RegistersServiceBusBus()
    {
        var config = Config(new()
        {
            ["Messaging:Provider"] = "ServiceBus",
            ["Messaging:ConnectionString"] =
                "Endpoint=sb://example.servicebus.windows.net/;SharedAccessKeyName=k;SharedAccessKey=abc123=",
        });

        var provider = new ServiceCollection().AddMessaging(config).BuildServiceProvider();

        // Construction must succeed without contacting Azure (no network until a send).
        provider.GetRequiredService<IMessageBus>().ShouldBeOfType<ServiceBusMessageBus>();
    }

    [Fact]
    public void ServiceBus_Provider_NoConnectionInfo_ThrowsOnResolve()
    {
        var config = Config(new() { ["Messaging:Provider"] = "ServiceBus" });
        var provider = new ServiceCollection().AddMessaging(config).BuildServiceProvider();

        Should.Throw<System.Exception>(() => provider.GetRequiredService<IMessageBus>());
    }
}
