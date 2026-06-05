using Mach.Application;
using Mach.Infrastructure.Commercetools;
using Mach.Infrastructure.Contentstack;
using Mach.Infrastructure.Email;
using Mach.Infrastructure.Messaging;
using Mach.Notifications.Functions;
using Mach.Persistence;
using Mach.ServiceDefaults;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureFunctionsWebApplication();

// Cross-cutting defaults (observability, health) + the application brain.
builder.Services.AddServiceDefaults(builder.Configuration);

// Application services — provides INotificationFanout (pure recipient/template resolution).
builder.Services.AddApplication();

// Vendor adapters: CMS email templates, transactional email, commerce reads (order/customer).
builder.Services.AddContentstack(builder.Configuration);
builder.Services.AddEmail(builder.Configuration);
builder.Services.AddCommercetools(builder.Configuration);

// Persistence — IFulfillmentDirectory (store/supplier/reception recipients) + IIdempotencyStore
// (per-audience send dedup so a Service Bus re-delivery doesn't resend mail).
var sqlConnectionString =
    builder.Configuration.GetConnectionString("Sql")
    ?? builder.Configuration["ConnectionStrings:Sql"]
    ?? throw new InvalidOperationException(
        "Missing SQL connection string. Set 'ConnectionStrings:Sql' (e.g. via local.settings.json or app settings).");
builder.Services.AddPersistence(sqlConnectionString);

// Messaging adapter (in-memory or Azure Service Bus per config). The Service Bus *trigger* itself
// is bound by the Functions runtime via the "ServiceBusConnection" app setting; this registration
// keeps IMessageBus available for any outbound publishing.
//
// NOTE: with Messaging:Provider=InMemory (offline, single-process) the SB trigger won't fire —
// expected. The trigger targets the Service Bus emulator / real Azure. Local email lands in the
// dev sink (./mail) when Email:Provider=DevSink.
builder.Services.AddMessaging(builder.Configuration);

// Notifications config (customer fallback recipient) + the fan-out orchestrator.
builder.Services.AddOptions<NotificationOptions>()
    .Bind(builder.Configuration.GetSection(NotificationOptions.SectionName));
builder.Services.AddScoped<OrderNotifier>();

builder.Build().Run();
