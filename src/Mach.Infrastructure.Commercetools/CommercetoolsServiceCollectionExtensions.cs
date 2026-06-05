using commercetools.Sdk.Api;

using Mach.Application.Ports;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;

namespace Mach.Infrastructure.Commercetools;

/// <summary>
/// Registers the commercetools adapter: the SDK API client (client-credentials service flow), the
/// OAuth2 token client for customer/anonymous/refresh flows, and the <see cref="ICommerceClient"/>
/// and <see cref="ICustomerAuth"/> implementations.
/// </summary>
public static class CommercetoolsServiceCollectionExtensions
{
    /// <summary>The SDK client name; also the in-memory config section the SDK binds from.</summary>
    private const string ClientName = "CommercetoolsApi";

    /// <summary>The named <see cref="HttpClient"/> used for the raw OAuth2 token flows.</summary>
    public const string TokenHttpClientName = "Commercetools.Auth";

    /// <summary>
    /// The named <see cref="HttpClient"/> used for the customer-token <c>/me</c> API path, where the
    /// caller's bearer token (not the service client-credentials token) authenticates the request.
    /// </summary>
    public const string CustomerApiHttpClientName = "Commercetools.CustomerApi";

    public static IServiceCollection AddCommercetools(
        this IServiceCollection services, IConfiguration config)
    {
        services.AddOptions<CommercetoolsOptions>()
            .Bind(config.GetSection(CommercetoolsOptions.SectionName))
            .Validate(o => !string.IsNullOrWhiteSpace(o.ProjectKey), "Commercetools:ProjectKey is required.")
            .Validate(o => !string.IsNullOrWhiteSpace(o.ClientId), "Commercetools:ClientId is required.")
            .Validate(o => !string.IsNullOrWhiteSpace(o.ClientSecret), "Commercetools:ClientSecret is required.")
            .Validate(o => !string.IsNullOrWhiteSpace(o.ApiUrl), "Commercetools:ApiUrl is required.")
            .Validate(o => !string.IsNullOrWhiteSpace(o.AuthUrl), "Commercetools:AuthUrl is required.");

        var options = config.GetSection(CommercetoolsOptions.SectionName).Get<CommercetoolsOptions>()
            ?? new CommercetoolsOptions();

        // The SDK binds its IClientConfiguration from a config section keyed by the client name. We
        // project our options onto the SDK's expected key names so a single Commercetools: section
        // drives both our adapter and the SDK client.
        var sdkConfig = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{ClientName}:ClientId"] = options.ClientId,
                [$"{ClientName}:ClientSecret"] = options.ClientSecret,
                [$"{ClientName}:Scope"] = options.ScopeList,
                [$"{ClientName}:ApiBaseAddress"] = EnsureTrailingSlash(options.ApiUrl),
                [$"{ClientName}:AuthorizationBaseAddress"] = EnsureTrailingSlash(options.AuthUrl),
                [$"{ClientName}:ProjectKey"] = options.ProjectKey,
            })
            .Build();

        // Registers IClient (client-credentials token provider) + ProjectApiRoot, and returns the
        // SDK's IHttpClientBuilder so we can attach a resilience pipeline for transient faults.
        services.UseCommercetoolsApi(sdkConfig, ClientName)
            .AddStandardResilienceHandler();

        // Dedicated, resilient HttpClient for the raw OAuth2 token endpoints.
        services.AddHttpClient(TokenHttpClientName)
            .AddStandardResilienceHandler();

        // Customer-token API path for /me and /me/login, with the same resilience handler as the
        // service path. The bearer token is set per request from the caller's access token.
        services.AddHttpClient(CustomerApiHttpClientName)
            .AddStandardResilienceHandler();

        services.AddSingleton<CommercetoolsTokenClient>(sp =>
        {
            var factory = sp.GetRequiredService<IHttpClientFactory>();
            var httpClient = factory.CreateClient(TokenHttpClientName);
            return new CommercetoolsTokenClient(httpClient, sp.GetRequiredService<IOptions<CommercetoolsOptions>>());
        });

        services.AddSingleton<CommercetoolsCustomerApiClient>(sp =>
        {
            var factory = sp.GetRequiredService<IHttpClientFactory>();
            var httpClient = factory.CreateClient(CustomerApiHttpClientName);
            return new CommercetoolsCustomerApiClient(httpClient, sp.GetRequiredService<IOptions<CommercetoolsOptions>>());
        });

        services.AddSingleton<ICommerceClient, CommercetoolsCommerceClient>();
        services.AddSingleton<ICustomerAuth>(sp => new CommercetoolsCustomerAuth(
            sp.GetRequiredService<CommercetoolsTokenClient>(),
            sp.GetRequiredService<commercetools.Sdk.Api.Client.ProjectApiRoot>(),
            sp.GetRequiredService<CommercetoolsCustomerApiClient>()));

        return services;
    }

    private static string EnsureTrailingSlash(string url) =>
        string.IsNullOrEmpty(url) || url.EndsWith('/') ? url : url + "/";
}
