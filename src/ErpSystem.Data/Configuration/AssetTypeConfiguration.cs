using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Maintenance;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ErpSystem.Data.Configuration;

public class AssetTypeConfiguration : IEntityTypeConfiguration<AssetType>
{
    public void Configure(EntityTypeBuilder<AssetType> builder)
    {
        builder.ToTable("AssetTypes");

        builder.HasKey(at => at.Id);

        builder.Property(at => at.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(at => at.Description)
            .HasMaxLength(500);

        builder.Property(at => at.Code)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(at => at.Icon)
            .HasMaxLength(50);

        builder.Property(at => at.Color)
            .HasMaxLength(7); // For hex colors like #FF5722

        builder.Property(at => at.DefaultMaintenanceIntervalDays)
            .HasDefaultValue(30);

        builder.Property(at => at.IsActive)
            .HasDefaultValue(true);

        builder.Property(at => at.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("GETUTCDATE()");

        builder.Property(at => at.UpdatedAt)
            .IsRequired()
            .HasDefaultValueSql("GETUTCDATE()");

        // Indexes
        builder.HasIndex(at => new { at.TenantId, at.Name }).IsUnique();
        builder.HasIndex(at => at.Code);
        builder.HasIndex(at => at.IsActive);

        // Relationships
        builder.HasOne(at => at.Tenant)
            .WithMany()
            .HasForeignKey(at => at.TenantId)
            .OnDelete(DeleteBehavior.Restrict);


        builder.HasMany(at => at.CustomFields)
            .WithOne(atf => atf.AssetType)
            .HasForeignKey(atf => atf.AssetTypeId)
            .OnDelete(DeleteBehavior.Cascade);

        // Removed relationship to MaintenanceAsset.AssetTypeId since we're using AssetCategoryId instead
    }
}

public class AssetTypeFieldConfiguration : IEntityTypeConfiguration<AssetTypeField>
{
    public void Configure(EntityTypeBuilder<AssetTypeField> builder)
    {
        builder.ToTable("AssetTypeFields");

        builder.HasKey(atf => atf.Id);

        builder.Property(atf => atf.FieldName)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(atf => atf.DisplayName)
            .IsRequired()
            .HasMaxLength(150);

        builder.Property(atf => atf.FieldType)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(atf => atf.DefaultValue)
            .HasMaxLength(500);

        builder.Property(atf => atf.ValidationRules)
            .HasColumnType("nvarchar(max)");

        builder.Property(atf => atf.Options)
            .HasColumnType("nvarchar(max)");

        builder.Property(atf => atf.HelpText)
            .HasMaxLength(500);

        builder.Property(atf => atf.IsRequired)
            .HasDefaultValue(false);

        builder.Property(atf => atf.IsActive)
            .HasDefaultValue(true);

        builder.Property(atf => atf.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("GETUTCDATE()");

        // Indexes
        builder.HasIndex(atf => new { atf.AssetTypeId, atf.FieldName }).IsUnique();
        builder.HasIndex(atf => atf.DisplayOrder);
        builder.HasIndex(atf => atf.IsActive);

        // Relationships
        builder.HasOne(atf => atf.AssetType)
            .WithMany(at => at.CustomFields)
            .HasForeignKey(atf => atf.AssetTypeId)
            .OnDelete(DeleteBehavior.Cascade);

        // Tenant relationship is handled by TenantEntity base class
    }
}
