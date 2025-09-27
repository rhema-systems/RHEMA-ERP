using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ErpSystem.Core.Entities;

namespace ErpSystem.Data.Configuration;

public class ApplicationUserConfiguration : IEntityTypeConfiguration<ApplicationUser>
{
    public void Configure(EntityTypeBuilder<ApplicationUser> builder)
    {
        // Configure the relationship between User and UserTenant
        builder.HasMany(u => u.UserTenants)
            .WithOne(ut => ut.User)
            .HasForeignKey(ut => ut.UserId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Restrict);

        // Primary tenant relationship
        builder.HasOne(u => u.Tenant)
            .WithMany()
            .HasForeignKey(u => u.TenantId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Restrict);

        // Add unique constraints
        builder.HasIndex(u => u.UserName)
            .IsUnique()
            .HasDatabaseName("IX_ApplicationUser_UserName_Unique");

        builder.HasIndex(u => u.Email)
            .IsUnique()
            .HasDatabaseName("IX_ApplicationUser_Email_Unique");

        // Ignore computed helper properties
        builder.Ignore(u => u.FullName);
        builder.Ignore(u => u.DefaultTenant);
        builder.Ignore(u => u.AccessibleTenants);
        builder.Ignore(u => u.ActiveTenantRelationships);
        builder.Ignore(u => u.SuspendedTenantRelationships);
        builder.Ignore(u => u.AllTenantRelationships);
    }
}