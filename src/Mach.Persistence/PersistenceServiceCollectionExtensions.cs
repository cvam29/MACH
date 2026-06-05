using Mach.Application.Ports;
using Mach.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Mach.Persistence;

/// <summary>DI registration for the MACH persistence layer.</summary>
public static class PersistenceServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="MachDbContext"/> (SQL Server) and the outbox / idempotency /
    /// projection stores.
    /// </summary>
    public static IServiceCollection AddPersistence(this IServiceCollection services, string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        services.AddDbContext<MachDbContext>(options =>
            options.UseSqlServer(connectionString, sql =>
                sql.MigrationsAssembly(typeof(MachDbContext).Assembly.FullName)));

        // Self-contained clock so the stores resolve a TimeProvider even when AddServiceDefaults
        // is not in play (e.g. integration tests that wire only persistence).
        services.TryAddSingleton(TimeProvider.System);

        services.AddScoped<IOutboxWriter, OutboxWriter>();
        services.AddScoped<IOutboxReader, OutboxReader>();
        services.AddScoped<IIdempotencyStore, IdempotencyStore>();
        services.AddScoped<IOrderProjectionStore, OrderProjectionStore>();
        services.AddScoped<IFulfillmentDirectory, FulfillmentDirectory>();

        return services;
    }
}
