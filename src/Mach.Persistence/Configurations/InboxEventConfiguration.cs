using Mach.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Mach.Persistence.Configurations;

internal sealed class InboxEventConfiguration : IEntityTypeConfiguration<InboxEventEntity>
{
    public void Configure(EntityTypeBuilder<InboxEventEntity> builder)
    {
        builder.ToTable("InboxEvents", MachSchemas.Messaging);

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedOnAdd();

        builder.Property(x => x.Source).IsRequired().HasMaxLength(128);
        builder.Property(x => x.DedupKey).IsRequired().HasMaxLength(256);
        builder.Property(x => x.ReceivedUtc).IsRequired();
        builder.Property(x => x.Status).IsRequired().HasMaxLength(64);
        builder.Property(x => x.RawPayload).IsRequired().HasColumnType("nvarchar(max)");

        builder.HasIndex(x => x.DedupKey).IsUnique();
    }
}
