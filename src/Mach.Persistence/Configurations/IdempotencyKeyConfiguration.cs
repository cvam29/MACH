using Mach.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Mach.Persistence.Configurations;

internal sealed class IdempotencyKeyConfiguration : IEntityTypeConfiguration<IdempotencyKeyEntity>
{
    public void Configure(EntityTypeBuilder<IdempotencyKeyEntity> builder)
    {
        builder.ToTable("IdempotencyKeys", MachSchemas.Idempotency);

        builder.HasKey(x => x.Key);
        builder.Property(x => x.Key).HasMaxLength(256).ValueGeneratedNever();

        builder.Property(x => x.RequestHash).IsRequired().HasMaxLength(128);
        builder.Property(x => x.ResponsePayload).HasColumnType("nvarchar(max)");
        builder.Property(x => x.State).IsRequired().HasMaxLength(32);
        builder.Property(x => x.CreatedUtc).IsRequired();
        builder.Property(x => x.ExpiresUtc).IsRequired();
    }
}
