using Mach.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Mach.Persistence.Configurations;

internal sealed class OrderProjectionConfiguration : IEntityTypeConfiguration<OrderProjectionEntity>
{
    public void Configure(EntityTypeBuilder<OrderProjectionEntity> builder)
    {
        builder.ToTable("OrderProjections", MachSchemas.Orders);

        builder.HasKey(x => x.OrderId);
        builder.Property(x => x.OrderId).HasMaxLength(128).ValueGeneratedNever();

        builder.Property(x => x.CustomerId).HasMaxLength(128);
        builder.Property(x => x.Number).IsRequired().HasMaxLength(128);
        builder.Property(x => x.Status).IsRequired().HasMaxLength(32);
        builder.Property(x => x.PaymentStatus).IsRequired().HasMaxLength(32);
        builder.Property(x => x.TotalGross).HasColumnType("decimal(18,2)");
        builder.Property(x => x.Currency).IsRequired().HasMaxLength(3);
        builder.Property(x => x.PlacedUtc).IsRequired();
        builder.Property(x => x.UpdatedUtc).IsRequired();
        builder.Property(x => x.RowVersion).IsRowVersion();

        builder.HasIndex(x => x.CustomerId);

        builder.HasMany(x => x.Lines)
            .WithOne(l => l.Order)
            .HasForeignKey(l => l.OrderId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class OrderLineProjectionConfiguration : IEntityTypeConfiguration<OrderLineProjectionEntity>
{
    public void Configure(EntityTypeBuilder<OrderLineProjectionEntity> builder)
    {
        builder.ToTable("OrderLineProjections", MachSchemas.Orders);

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedOnAdd();

        builder.Property(x => x.OrderId).IsRequired().HasMaxLength(128);
        builder.Property(x => x.Sku).IsRequired().HasMaxLength(100);
        builder.Property(x => x.Name).IsRequired().HasMaxLength(512);
        builder.Property(x => x.Quantity).IsRequired();
        builder.Property(x => x.UnitPriceGross).HasColumnType("decimal(18,2)");
        builder.Property(x => x.Currency).IsRequired().HasMaxLength(3);

        builder.HasIndex(x => x.OrderId);
    }
}
