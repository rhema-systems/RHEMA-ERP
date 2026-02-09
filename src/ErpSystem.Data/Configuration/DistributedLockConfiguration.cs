using ErpSystem.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ErpSystem.Data.Configuration;

public sealed class DistributedLockConfiguration : IEntityTypeConfiguration<DistributedLock>
{
    public void Configure(EntityTypeBuilder<DistributedLock> builder)
    {
        builder.ToTable("DistributedLocks");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.LockName)
            .IsRequired()
            .HasMaxLength(200);

        builder.HasIndex(x => x.LockName)
            .IsUnique();

        builder.Property(x => x.AcquiredBy)
            .HasMaxLength(200);

        builder.Property(x => x.LeaseUntilUtc)
            .IsRequired();
    }
}

