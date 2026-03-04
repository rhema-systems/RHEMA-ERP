using ErpSystem.Core.Entities.Maintenance;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ErpSystem.Data.Configuration.Maintenance;

public sealed class FleetVehicleComplianceTemplateConfiguration : IEntityTypeConfiguration<FleetVehicleComplianceTemplate>
{
    public void Configure(EntityTypeBuilder<FleetVehicleComplianceTemplate> builder)
    {
        builder.ToTable("FleetVehicleComplianceTemplates");

        builder.HasIndex(x => new { x.TenantId, x.VehicleAssetId })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0");
        builder.HasIndex(x => new { x.TenantId, x.TemplateId });

        builder.HasOne(x => x.VehicleAsset)
            .WithMany()
            .HasForeignKey(x => x.VehicleAssetId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Template)
            .WithMany()
            .HasForeignKey(x => x.TemplateId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
