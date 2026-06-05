using System;
using Azure.Messaging.ServiceBus;
using Mach.Infrastructure.Messaging;
using Microsoft.Extensions.Options;
using Shouldly;

namespace Mach.Infrastructure.Messaging.Tests;

/// <summary>
/// Exercises construction and sender-caching logic without connecting to a live namespace.
/// The Service Bus client is lazy: creating a client/sender does not open a network connection
/// until the first send, so these assertions are safe offline.
/// </summary>
public sealed class ServiceBusMessageBusTests
{
    private const string FakeConnectionString =
        "Endpoint=sb://example.servicebus.windows.net/;SharedAccessKeyName=k;SharedAccessKey=abc123=";

    [Fact]
    public void Construct_WithConnectionString_Succeeds()
    {
        var options = Options.Create(new MessagingOptions
        {
            Provider = MessagingProvider.ServiceBus,
            ConnectionString = FakeConnectionString,
        });

        var bus = new ServiceBusMessageBus(options);
        bus.ShouldNotBeNull();
    }

    [Fact]
    public void Construct_WithNamespace_Succeeds()
    {
        var options = Options.Create(new MessagingOptions
        {
            Provider = MessagingProvider.ServiceBus,
            Namespace = "example.servicebus.windows.net",
        });

        var bus = new ServiceBusMessageBus(options);
        bus.ShouldNotBeNull();
    }

    [Fact]
    public void Construct_WithNoConnectionInfo_Throws()
    {
        var options = Options.Create(new MessagingOptions { Provider = MessagingProvider.ServiceBus });

        Should.Throw<InvalidOperationException>(() => new ServiceBusMessageBus(options));
    }

    [Fact]
    public async Task GetSender_CachesPerTopic()
    {
        await using var client = new ServiceBusClient(FakeConnectionString);
        await using var bus = new ServiceBusMessageBus(client);

        var ordersA = bus.GetSender("orders");
        var ordersB = bus.GetSender("orders");
        var stock = bus.GetSender("stock");

        ReferenceEquals(ordersA, ordersB).ShouldBeTrue();
        ReferenceEquals(ordersA, stock).ShouldBeFalse();
        ordersA.EntityPath.ShouldBe("orders");
        stock.EntityPath.ShouldBe("stock");
    }
}
