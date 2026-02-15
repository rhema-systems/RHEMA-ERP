using ErpSystem.Core.Entities.Maintenance;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ErpSystem.Data.Configuration.Maintenance;

public sealed class FleetExternalRepairConfiguration : IEntityTypeConfiguration<FleetExternalRepair>
{
    public void Configure(EntityTypeBuilder<FleetExternalRepair> builder)
    {
        builder.ToTable("FleetExternalRepairs");

        builder.HasIndex(x => new { x.TenantId, x.VehicleAssetId, x.Status, x.IsDeleted });
        builder.HasIndex(x => new { x.TenantId, x.VendorBusinessPartnerId, x.IsDeleted });

        builder.Property(x => x.Status).HasMaxLength(20);
        builder.Property(x => x.CurrencyCode).HasMaxLength(10);
    }
}

