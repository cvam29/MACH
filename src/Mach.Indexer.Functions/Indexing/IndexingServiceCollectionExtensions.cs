using Microsoft.Extensions.DependencyInjection;

namespace Mach.Indexer.Functions.Indexing;

/// <summary>Registers the Indexer host's own handler services (no trigger types involved).</summary>
public static class IndexingServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="CacheInvalidator"/>, <see cref="ProductIndexer"/> and
    /// <see cref="CatalogReconciler"/>. Each depends only on application ports, so they are unit
    /// testable without Service Bus or the Functions host.
    /// </summary>
    public static IServiceCollection AddIndexing(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<CacheInvalidator>();
        services.AddSingleton<ProductIndexer>();
        services.AddSingleton<CatalogReconciler>();

        return services;
    }
}
