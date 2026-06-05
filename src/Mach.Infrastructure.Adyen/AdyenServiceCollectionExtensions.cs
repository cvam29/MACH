using Mach.Application.Ports;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using HmacValidator = Adyen.Util.HmacValidator;

namespace Mach.Infrastructure.Adyen;

/// <summary>DI registration for the Adyen payment gateway adapter.</summary>
public static class AdyenServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Adyen <see cref="IPaymentGateway"/> implementation and its dependencies,
    /// binding configuration from the <c>Adyen</c> section.
    /// </summary>
    public static IServiceCollection AddAdyen(this IServiceCollection services, IConfiguration config)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(config);

        services.AddOptions<AdyenOptions>()
            .Bind(config.GetSection(AdyenOptions.SectionName));

        services.AddSingleton<HmacValidator>();

        services.AddHttpClient<IAdyenCheckoutApi, HttpAdyenCheckoutApi>();

        services.AddScoped<IPaymentGateway, AdyenPaymentGateway>();

        return services;
    }
}
