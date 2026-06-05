using Mach.Infrastructure.Commercetools;
using Mach.Infrastructure.Messaging;
using Mach.Persistence;
using Mach.Projection.Functions;
using Mach.ServiceDefaults;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureFunctionsWebApplication();

// AddServiceDefaults also calls AddApplication() (MediatR + validators + app services),
// so the application layer does not need to be registered separately.
builder.Services.AddServiceDefaults(builder.Configuration);

// Commerce engine adapter (ICommerceClient) + read-model store (IOrderProjectionStore).
builder.Services.AddCommercetools(builder.Configuration);
builder.Services.AddPersistence(
    builder.Configuration.GetConnectionString("Sql")
    ?? throw new InvalidOperationException("ConnectionStrings:Sql is required."));

// Messaging adapter (IMessageBus). The Projection host is driven by the Service Bus *trigger*
// (the SB extension binding), not by IMessageBus directly; AddMessaging is wired for parity with
// the other hosts and so the bus is available if the host needs to publish.
//
// Offline note: with Messaging:Provider=InMemory there is no Service Bus, so the
// PaymentProjectionFunction trigger will not fire — that is expected. The trigger path targets the
// Service Bus emulator or a real Azure Service Bus namespace.
builder.Services.AddMessaging(builder.Configuration);

// The Service Bus function delegates to this plain, unit-testable handler.
builder.Services.AddScoped<PaymentProjector>();

builder.Build().Run();
