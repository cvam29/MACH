using Mach.Application.Ports;
using Mach.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

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

        services.AddScoped<IOutboxWriter, OutboxWriter>();
        services.AddScoped<IOutboxReader, OutboxReader>();
        services.AddScoped<IIdempotencyStore, IdempotencyStore>();
        services.AddScoped<IOrderProjectionStore, OrderProjectionStore>();
        services.AddScoped<IFulfillmentDirectory, FulfillmentDirectory>();

        return services;
    }
}
