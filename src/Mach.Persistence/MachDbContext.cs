using Mach.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace Mach.Persistence;

/// <summary>
/// The MACH persistence root (EF Core 10). Uses schema-per-concern: messaging,
/// idempotency, orders, customers, fulfillment, notifications, audit.
/// </summary>
public sealed class MachDbContext(DbContextOptions<MachDbContext> options) : DbContext(options)
{
    public DbSet<OutboxMessageEntity> OutboxMessages => Set<OutboxMessageEntity>();
    public DbSet<InboxEventEntity> InboxEvents => Set<InboxEventEntity>();
    public DbSet<IdempotencyKeyEntity> IdempotencyKeys => Set<IdempotencyKeyEntity>();
    public DbSet<OrderProjectionEntity> OrderProjections => Set<OrderProjectionEntity>();
    public DbSet<OrderLineProjectionEntity> OrderLineProjections => Set<OrderLineProjectionEntity>();
    public DbSet<ProfileCacheEntity> ProfileCache => Set<ProfileCacheEntity>();
    public DbSet<StoreEntity> Stores => Set<StoreEntity>();
    public DbSet<SupplierEntity> Suppliers => Set<SupplierEntity>();
    public DbSet<ProductSupplierEntity> ProductSuppliers => Set<ProductSupplierEntity>();
    public DbSet<EmailDeliveryEntity> EmailDeliveries => Set<EmailDeliveryEntity>();
    public DbSet<WebhookDeliveryEntity> WebhookDeliveries => Set<WebhookDeliveryEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(MachDbContext).Assembly);
    }
}
