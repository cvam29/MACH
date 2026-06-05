using Mach.Application.Ports;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;

namespace Mach.Infrastructure.Contentstack;

/// <summary>DI registration for the Contentstack CDA adapter.</summary>
public static class ContentstackServiceCollectionExtensions
{
    /// <summary>The logical name of the typed Contentstack <see cref="HttpClient"/>.</summary>
    public const string HttpClientName = "contentstack";

    /// <summary>
    /// Registers <see cref="ContentstackOptions"/> (bound from the <c>Contentstack:</c> section),
    /// the typed Contentstack <see cref="HttpClient"/> with standard resilience, and
    /// <see cref="ContentstackCmsClient"/> as the <see cref="ICmsClient"/> implementation.
    /// </summary>
    public static IServiceCollection AddContentstack(
        this IServiceCollection services, IConfiguration config)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(config);

        services
            .AddOptions<ContentstackOptions>()
            .Bind(config.GetSection(ContentstackOptions.SectionName))
            .Validate(
                o => o.IsValid,
                "Contentstack requires ApiKey, DeliveryToken and Environment to be configured.")
            .ValidateOnStart();

        services.AddHttpClient<ICmsClient, ContentstackCmsClient>(HttpClientName, (sp, client) =>
        {
            var options = sp.GetRequiredService<
                Microsoft.Extensions.Options.IOptions<ContentstackOptions>>().Value;

            client.BaseAddress = new Uri(options.ResolveBaseUrl());
            client.DefaultRequestHeaders.Add("api_key", options.ApiKey);
            client.DefaultRequestHeaders.Add("access_token", options.DeliveryToken);
            client.DefaultRequestHeaders.Add("environment", options.Environment);
        })
        .AddStandardResilienceHandler();

        return services;
    }
}
