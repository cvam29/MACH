using Mach.Application;
using Mach.Infrastructure.Messaging;
using Mach.Outbox.Functions;
using Mach.Persistence;
using Mach.ServiceDefaults;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureFunctionsWebApplication();

builder.Services.AddServiceDefaults(builder.Configuration);

// Application brain (MediatR handlers, validators, services).
builder.Services.AddApplication();

// Persistence adapter — provides IOutboxReader over SQL Server.
var sqlConnectionString =
    builder.Configuration.GetConnectionString("Sql")
    ?? builder.Configuration["ConnectionStrings:Sql"]
    ?? throw new InvalidOperationException(
        "Missing SQL connection string. Set 'ConnectionStrings:Sql' (e.g. via local.settings.json or app settings).");
builder.Services.AddPersistence(sqlConnectionString);

// Messaging adapter — provides IMessageBus (in-memory or Azure Service Bus per config).
builder.Services.AddMessaging(builder.Configuration);

// The outbox dispatch loop the timer function delegates to.
builder.Services.AddScoped<OutboxDispatcher>();

builder.Build().Run();
