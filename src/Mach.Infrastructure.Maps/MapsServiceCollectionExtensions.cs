namespace Mach.Infrastructure.Maps;

using System.Net.Http.Headers;
using Mach.Application.Ports;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

/// <summary>
/// DI wiring for the Maps adapter. Selects the <see cref="IGeoLocator"/> implementation
/// from the <c>Maps:Provider</c> configuration value.
/// </summary>
public static class MapsServiceCollectionExtensions
{
    /// <summary>
    /// Registers an <see cref="IGeoLocator"/> based on <c>Maps:Provider</c>
    /// (<c>Azure</c> or <c>Stub</c>, defaulting to <c>Stub</c>).
    /// </summary>
    public static IServiceCollection AddMaps(this IServiceCollection services, IConfiguration config)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(config);

        var section = config.GetSection(MapsOptions.SectionName);
        services.Configure<MapsOptions>(section);

        var options = section.Get<MapsOptions>() ?? new MapsOptions();

        if (options.Provider == MapsProvider.Azure)
        {
            services.AddHttpClient<IGeoLocator, AzureMapsGeoLocator>(static (sp, client) =>
                {
                    var opts = sp.GetRequiredService<IOptions<MapsOptions>>().Value;

                    if (string.IsNullOrWhiteSpace(opts.SubscriptionKey))
                    {
                        throw new InvalidOperationException(
                            "Maps:SubscriptionKey is required when Maps:Provider is 'Azure'.");
                    }

                    client.BaseAddress = new Uri(EnsureTrailingSlash(opts.BaseUrl));
                    client.DefaultRequestHeaders.Add("subscription-key", opts.SubscriptionKey);
                    client.DefaultRequestHeaders.Accept.Add(
                        new MediaTypeWithQualityHeaderValue("application/json"));
                })
                .AddStandardResilienceHandler();
        }
        else
        {
            services.AddSingleton<IGeoLocator, StubGeoLocator>();
        }

        return services;
    }

    private static string EnsureTrailingSlash(string url)
        => url.EndsWith('/') ? url : url + "/";
}
