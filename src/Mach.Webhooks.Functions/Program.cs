using Mach.Infrastructure.Adyen;
using Mach.Infrastructure.Messaging;
using Mach.Persistence;
using Mach.ServiceDefaults;
using Mach.Webhooks.Functions;

using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureFunctionsWebApplication();

// Service defaults wire the application layer (AddApplication), observability and health checks.
builder.Services.AddServiceDefaults(builder.Configuration);

// Inbound vendor adapters + infrastructure ports the webhook handlers depend on.
//   AddAdyen      → IPaymentGateway (HMAC verify + notification parse)
//   AddPersistence→ IIdempotencyStore (inbox/dedup)
//   AddMessaging  → IMessageBus (topic publish)
builder.Services.AddAdyen(builder.Configuration);
builder.Services.AddMessaging(builder.Configuration);

var sqlConnectionString =
    builder.Configuration.GetConnectionString("Sql")
    ?? builder.Configuration["SQL_CONNECTION_STRING"]
    ?? builder.Configuration["ConnectionStrings:Sql"]
    ?? throw new InvalidOperationException(
        "No SQL connection string configured. Set 'ConnectionStrings:Sql' or 'SQL_CONNECTION_STRING'.");

builder.Services.AddPersistence(sqlConnectionString);

builder.Services.AddOptions<ContentstackWebhookOptions>()
    .Bind(builder.Configuration.GetSection(ContentstackWebhookOptions.SectionName));

builder.Build().Run();
