using Mach.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Mach.Persistence.Configurations;

internal sealed class EmailDeliveryConfiguration : IEntityTypeConfiguration<EmailDeliveryEntity>
{
    public void Configure(EntityTypeBuilder<EmailDeliveryEntity> builder)
    {
        builder.ToTable("EmailDeliveries", MachSchemas.Notifications);

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedOnAdd();

        builder.Property(x => x.OrderId).IsRequired().HasMaxLength(128);
        builder.Property(x => x.Audience).IsRequired().HasMaxLength(32);
        builder.Property(x => x.Kind).IsRequired().HasMaxLength(64);
        builder.Property(x => x.ProviderMessageId).HasMaxLength(256);
        builder.Property(x => x.Status).IsRequired().HasMaxLength(32);
        builder.Property(x => x.SentUtc).IsRequired();

        // One delivery per (order, audience, kind) — dedupes multi-party fan-out.
        builder.HasIndex(x => new { x.OrderId, x.Audience, x.Kind }).IsUnique();
    }
}
