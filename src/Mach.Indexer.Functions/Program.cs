using Mach.Indexer.Functions.Indexing;
using Mach.Infrastructure.Algolia;
using Mach.Infrastructure.Caching;
using Mach.Infrastructure.Commercetools;
using Mach.Infrastructure.Contentstack;
using Mach.Infrastructure.Messaging;
using Mach.ServiceDefaults;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureFunctionsWebApplication();

// Application layer + observability + health (also calls AddApplication()).
builder.Services.AddServiceDefaults(builder.Configuration);

// Vendor / infrastructure adapters this host depends on.
builder.Services.AddAlgolia(builder.Configuration);          // ISearchClient
builder.Services.AddCommercetools(builder.Configuration);    // ICommerceClient
builder.Services.AddContentstack(builder.Configuration);     // ICmsClient
builder.Services.AddCaching(builder.Configuration);          // ICacheStore
builder.Services.AddMessaging(builder.Configuration);        // IMessageBus

// The Indexer host's own handler services (ProductIndexer, CacheInvalidator, CatalogReconciler).
builder.Services.AddIndexing();

builder.Build().Run();
