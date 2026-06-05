using Algolia.Search.Clients;
using Algolia.Search.Transport;

using Mach.Application.Ports;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using AlgoliaClient = Algolia.Search.Clients.ISearchClient;

namespace Mach.Infrastructure.Algolia;

/// <summary>
/// DI registration for the Algolia indexing adapter.
/// </summary>
public static class AlgoliaServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Algolia <see cref="ISearchClient"/> adapter and its underlying client,
    /// binding configuration from the <c>Algolia:</c> section.
    /// </summary>
    public static IServiceCollection AddAlgolia(this IServiceCollection services, IConfiguration config)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(config);

        services.AddOptions<AlgoliaOptions>()
            .Bind(config.GetSection(AlgoliaOptions.SectionName))
            .Validate(
                o => !string.IsNullOrWhiteSpace(o.AppId), "Algolia:AppId is required.")
            .Validate(
                o => !string.IsNullOrWhiteSpace(o.AdminApiKey), "Algolia:AdminApiKey is required.")
            .Validate(
                o => !string.IsNullOrWhiteSpace(o.IndexName), "Algolia:IndexName is required.")
            .ValidateOnStart();

        // The low-level Algolia client. Built from validated options; honours an optional custom
        // host override (used for testing against a mock transport).
        services.AddSingleton<AlgoliaClient>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<AlgoliaOptions>>().Value;
            var searchConfig = BuildConfig(options);
            return new SearchClient(searchConfig);
        });

        services.AddSingleton<Mach.Application.Ports.ISearchClient, AlgoliaSearchClient>();

        return services;
    }

    /// <summary>
    /// Builds the <see cref="SearchConfig"/> from options, including resilience timeouts and any
    /// custom host overrides. Exposed for tests that need a client pointed at a mock host.
    /// </summary>
    public static SearchConfig BuildConfig(AlgoliaOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var searchConfig = new SearchConfig(options.AppId, options.AdminApiKey);

        if (options.WriteTimeoutSeconds > 0)
        {
            searchConfig.WriteTimeout = TimeSpan.FromSeconds(options.WriteTimeoutSeconds);
        }

        if (options.CustomHosts is { Count: > 0 })
        {
            searchConfig.CustomHosts = options.CustomHosts
                .Select(ParseHost)
                .ToList();
        }

        return searchConfig;
    }

    private static StatefulHost ParseHost(string host)
    {
        if (string.IsNullOrWhiteSpace(host))
        {
            throw new ArgumentException("Custom host entries must be non-empty.", nameof(host));
        }

        // Accept either a bare host ("localhost:8080") or a full URL ("http://localhost:8080").
        var scheme = HttpScheme.Https;
        var value = host.Trim();

        if (value.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
        {
            scheme = HttpScheme.Http;
            value = value["http://".Length..];
        }
        else if (value.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            value = value["https://".Length..];
        }

        value = value.TrimEnd('/');

        int? port = null;
        var colon = value.LastIndexOf(':');
        if (colon > 0 && int.TryParse(value[(colon + 1)..], out var parsedPort))
        {
            port = parsedPort;
            value = value[..colon];
        }

        return new StatefulHost
        {
            Url = value,
            Scheme = scheme,
            Port = port,
            Up = true,
            RetryCount = 0,
            LastUse = DateTime.UtcNow,
            Accept = CallType.Read | CallType.Write,
        };
    }
}
