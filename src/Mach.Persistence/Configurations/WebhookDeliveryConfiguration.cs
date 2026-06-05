using Mach.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Mach.Persistence.Configurations;

internal sealed class WebhookDeliveryConfiguration : IEntityTypeConfiguration<WebhookDeliveryEntity>
{
    public void Configure(EntityTypeBuilder<WebhookDeliveryEntity> builder)
    {
        builder.ToTable("WebhookDeliveries", MachSchemas.Audit);

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedOnAdd();

        builder.Property(x => x.Source).IsRequired().HasMaxLength(128);
        builder.Property(x => x.ReceivedUtc).IsRequired();
        builder.Property(x => x.Status).IsRequired().HasMaxLength(32);
        builder.Property(x => x.LatencyMs).IsRequired();
        builder.Property(x => x.SignatureValid).IsRequired();
        builder.Property(x => x.Notes).HasMaxLength(1024);

        builder.HasIndex(x => x.ReceivedUtc);
    }
}
