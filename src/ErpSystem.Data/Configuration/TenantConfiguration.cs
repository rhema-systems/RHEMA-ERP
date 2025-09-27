using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ErpSystem.Core.Entities;

namespace ErpSystem.Data.Configuration;

public class TenantConfiguration : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> builder)
    {
        builder.Property(t => t.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(t => t.Code)
            .IsRequired()
            .HasMaxLength(50);

        builder.HasIndex(t => t.Code)
            .IsUnique();

        builder.HasIndex(t => t.Domain)
            .IsUnique()
            .HasFilter("[Domain] IS NOT NULL");

        builder.HasMany(t => t.UserTenants)
            .WithOne(ut => ut.Tenant)
            .HasForeignKey(ut => ut.TenantId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Restrict);
    }
}