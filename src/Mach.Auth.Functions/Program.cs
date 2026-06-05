using Mach.ServiceDefaults;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureFunctionsWebApplication();

builder.Services.AddServiceDefaults(builder.Configuration);

// TODO (Wave 2): register infrastructure adapters and add the Auth host's functions.

builder.Build().Run();
