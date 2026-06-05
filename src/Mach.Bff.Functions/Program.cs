using Mach.Application;
using Mach.Application.Services;
using Mach.Bff.Functions;
using Mach.Infrastructure.Adyen;
using Mach.Infrastructure.Caching;
using Mach.Infrastructure.Commercetools;
using Mach.Infrastructure.Contentstack;
using Mach.Infrastructure.Maps;
using Mach.Infrastructure.Messaging;
using Mach.Persistence;
using Mach.ServiceDefaults;

using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureFunctionsWebApplication();

// Credentialed CORS for the storefront origin(s); short-circuits preflight (OPTIONS).
builder.UseMiddleware<CorsMiddleware>();

// Observability, health checks and the application brain (MediatR/validators/services).
builder.Services.AddServiceDefaults(builder.Configuration);

// Explicit application registration (idempotent with AddServiceDefaults), per host convention.
builder.Services.AddApplication();

// Vendor adapters (ports → implementations).
builder.Services.AddCommercetools(builder.Configuration); // ICommerceClient, ICustomerAuth
builder.Services.AddContentstack(builder.Configuration);   // ICmsClient
builder.Services.AddMaps(builder.Configuration);           // IGeoLocator
builder.Services.AddAdyen(builder.Configuration);          // IPaymentGateway
builder.Services.AddCaching(builder.Configuration);        // ICacheStore
builder.Services.AddMessaging(builder.Configuration);      // IMessageBus

// Persistence adapter — IFulfillmentDirectory, outbox, idempotency, order projections.
var sqlConnectionString =
    builder.Configuration.GetConnectionString("Sql")
    ?? builder.Configuration["ConnectionStrings:Sql"]
    ?? throw new InvalidOperationException(
        "Missing SQL connection string. Set 'ConnectionStrings:Sql' (e.g. via local.settings.json or app settings).");
builder.Services.AddPersistence(sqlConnectionString);

// Bind the distance-based delivery quoting rules (defaults apply when the section is absent).
builder.Services.AddOptions<DeliveryQuotingOptions>()
    .Bind(builder.Configuration.GetSection(DeliveryQuotingOptions.SectionName));

// CORS + session-cookie reading config for the storefront BFF surface.
builder.Services.AddOptions<CorsOptions>()
    .Bind(builder.Configuration.GetSection(CorsOptions.SectionName));
builder.Services.AddOptions<SessionCookieOptions>()
    .Bind(builder.Configuration.GetSection(SessionCookieOptions.SectionName));

builder.Services.AddSingleton<CookieReader>();

builder.Build().Run();
