using Mach.Application.Ports;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using AdyenCheckoutExtensions = Adyen.Checkout.Extensions.ServiceCollectionExtensions;
using AdyenEnvironment = Adyen.Core.Options.AdyenEnvironment;
using HmacValidator = Adyen.Util.HmacValidator;
using HostConfiguration = Adyen.Checkout.Client.HostConfiguration;

namespace Mach.Infrastructure.Adyen;

/// <summary>DI registration for the Adyen payment gateway adapter.</summary>
public static class AdyenServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Adyen <see cref="IPaymentGateway"/> implementation and its dependencies,
    /// binding configuration from the <c>Adyen</c> section.
    /// </summary>
    /// <param name="services">The service collection to add registrations to.</param>
    /// <param name="config">Application configuration providing the <c>Adyen</c> section.</param>
    /// <param name="configureHttpClient">
    /// Optional hook to customize the <see cref="HttpClient"/> the Adyen SDK's checkout service uses
    /// (e.g. to point integration tests at a WireMock server by setting its
    /// <see cref="HttpClient.BaseAddress"/>, or to swap in a custom message handler). This injects
    /// through the Adyen SDK's own configuration surface and does NOT replace the typed SDK client.
    /// </param>
    public static IServiceCollection AddAdyen(
        this IServiceCollection services,
        IConfiguration config,
        Action<HttpClient>? configureHttpClient = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(config);

        services.AddOptions<AdyenOptions>()
            .Bind(config.GetSection(AdyenOptions.SectionName));

        services.AddSingleton<HmacValidator>();

        // Read the Adyen settings directly from configuration so we can hand them to the SDK's own
        // HostConfiguration without prematurely building a service provider.
        var section = config.GetSection(AdyenOptions.SectionName);
        var apiKey = section[nameof(AdyenOptions.ApiKey)] ?? string.Empty;
        var hmacKey = section[nameof(AdyenOptions.HmacKey)] ?? string.Empty;
        var environment = section[nameof(AdyenOptions.Environment)] ?? "Test";

        // Configure the Adyen SDK's checkout options (environment + API key) through the SDK's own
        // HostConfiguration surface, then register its typed checkout service (IPaymentsService),
        // which exposes the Sessions API. The HttpClient hook below lets tests redirect the SDK to a
        // WireMock endpoint without any hand-rolled REST client.
        new HostConfiguration(services).ConfigureAdyenOptions(o =>
        {
            o.Environment = string.Equals(environment, "Live", StringComparison.OrdinalIgnoreCase)
                ? AdyenEnvironment.Live
                : AdyenEnvironment.Test;
            o.AdyenApiKey = apiKey;
            o.AdyenHmacKey = hmacKey;
        });

        AdyenCheckoutExtensions.AddPaymentsService(
            services,
            serviceLifetime: ServiceLifetime.Scoped,
            httpClientOptions: configureHttpClient is null ? null : configureHttpClient.Invoke,
            httpClientBuilderOptions: null);

        services.AddScoped<IPaymentGateway, AdyenPaymentGateway>();

        return services;
    }
}
