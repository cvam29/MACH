using Mach.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Mach.Persistence.Configurations;

internal sealed class StoreConfiguration : IEntityTypeConfiguration<StoreEntity>
{
    public void Configure(EntityTypeBuilder<StoreEntity> builder)
    {
        builder.ToTable("Stores", MachSchemas.Fulfillment);

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();

        // EXACT column names — seed/sql/fulfillment-seed.sql depends on them.
        builder.Property(x => x.Name).IsRequired().HasMaxLength(200);
        builder.Property(x => x.Email).IsRequired().HasMaxLength(320);
        builder.Property(x => x.ReceptionEmail).IsRequired().HasMaxLength(320);
        builder.Property(x => x.Lat); // float
        builder.Property(x => x.Lng); // float

        builder.HasIndex(x => x.Name).IsUnique();
    }
}

internal sealed class SupplierConfiguration : IEntityTypeConfiguration<SupplierEntity>
{
    public void Configure(EntityTypeBuilder<SupplierEntity> builder)
    {
        builder.ToTable("Suppliers", MachSchemas.Fulfillment);

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();

        builder.Property(x => x.Name).IsRequired().HasMaxLength(200);
        builder.Property(x => x.Email).IsRequired().HasMaxLength(320);

        builder.HasIndex(x => x.Name).IsUnique();

        builder.HasMany(x => x.Products)
            .WithOne(p => p.Supplier)
            .HasForeignKey(p => p.SupplierId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class ProductSupplierConfiguration : IEntityTypeConfiguration<ProductSupplierEntity>
{
    public void Configure(EntityTypeBuilder<ProductSupplierEntity> builder)
    {
        builder.ToTable("ProductSuppliers", MachSchemas.Fulfillment);

        // A SKU maps to a single supplier; Sku is the natural key (seed MERGEs on it).
        builder.HasKey(x => x.Sku);
        builder.Property(x => x.Sku).HasMaxLength(100).ValueGeneratedNever();

        builder.Property(x => x.SupplierId).IsRequired();

        builder.HasIndex(x => x.SupplierId);
    }
}
