using Mach.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Mach.Persistence.Configurations;

internal sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessageEntity>
{
    public void Configure(EntityTypeBuilder<OutboxMessageEntity> builder)
    {
        builder.ToTable("OutboxMessages", MachSchemas.Messaging);

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedOnAdd();

        builder.Property(x => x.OccurredUtc).IsRequired();
        builder.Property(x => x.Topic).IsRequired().HasMaxLength(256);
        builder.Property(x => x.Type).IsRequired().HasMaxLength(512);
        builder.Property(x => x.Payload).IsRequired().HasColumnType("nvarchar(max)");
        builder.Property(x => x.ProcessedUtc);
        builder.Property(x => x.Attempts).IsRequired();
        builder.Property(x => x.Error);

        // Dispatcher queries unsent rows oldest-first.
        builder.HasIndex(x => new { x.ProcessedUtc, x.OccurredUtc });
    }
}
