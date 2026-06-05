using Mach.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Mach.Persistence.Configurations;

internal sealed class ProfileCacheConfiguration : IEntityTypeConfiguration<ProfileCacheEntity>
{
    public void Configure(EntityTypeBuilder<ProfileCacheEntity> builder)
    {
        builder.ToTable("ProfileCache", MachSchemas.Customers);

        builder.HasKey(x => x.CustomerId);
        builder.Property(x => x.CustomerId).HasMaxLength(128).ValueGeneratedNever();

        builder.Property(x => x.Email).IsRequired().HasMaxLength(320);
        builder.Property(x => x.DisplayName).IsRequired().HasMaxLength(256);
        builder.Property(x => x.LoyaltyTier).HasMaxLength(64);
        builder.Property(x => x.RefreshedUtc).IsRequired();
    }
}
