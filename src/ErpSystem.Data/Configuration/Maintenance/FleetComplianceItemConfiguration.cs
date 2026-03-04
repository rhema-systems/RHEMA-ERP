using ErpSystem.Core.Entities.Maintenance;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ErpSystem.Data.Configuration.Maintenance;

public sealed class FleetComplianceItemConfiguration : IEntityTypeConfiguration<FleetComplianceItem>
{
    public void Configure(EntityTypeBuilder<FleetComplianceItem> builder)
    {
        builder.ToTable("FleetComplianceItems");

        builder.HasIndex(x => new { x.TenantId, x.VehicleAssetId });
        builder.HasIndex(x => new { x.TenantId, x.ExpiryDate });
        builder.HasIndex(x => new { x.TenantId, x.VehicleAssetId, x.IsCritical, x.ExpiryDate });
        builder.HasIndex(x => new { x.TenantId, x.VehicleAssetId, x.TemplateItemId })
            .IsUnique()
            .HasFilter("[TemplateItemId] IS NOT NULL AND [IsDeleted] = 0");

        builder.HasOne(x => x.VehicleAsset)
            .WithMany()
            .HasForeignKey(x => x.VehicleAssetId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.TemplateItem)
            .WithMany()
            .HasForeignKey(x => x.TemplateItemId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

