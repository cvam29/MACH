using Mach.Auth.Functions;
using Mach.Infrastructure.Commercetools;
using Mach.ServiceDefaults;

using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureFunctionsWebApplication();

// Credentialed CORS for the storefront origin(s), and preflight short-circuiting.
builder.UseMiddleware<CorsMiddleware>();

builder.Services.AddServiceDefaults(builder.Configuration);

// commercetools adapter → ICustomerAuth and the OAuth2 token flows.
builder.Services.AddCommercetools(builder.Configuration);

builder.Services.AddOptions<AuthCookieOptions>()
    .Bind(builder.Configuration.GetSection(AuthCookieOptions.SectionName));

builder.Services.AddOptions<CorsOptions>()
    .Bind(builder.Configuration.GetSection(CorsOptions.SectionName));

builder.Services.AddSingleton<AuthCookieWriter>();

builder.Build().Run();
