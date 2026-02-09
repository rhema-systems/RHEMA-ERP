using ErpSystem.Core.Entities.Maintenance;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ErpSystem.Data.Configuration.Maintenance;

public sealed class FleetBatteryEventConfiguration : IEntityTypeConfiguration<FleetBatteryEvent>
{
    public void Configure(EntityTypeBuilder<FleetBatteryEvent> builder)
    {
        builder.ToTable("FleetBatteryEvents");

        builder.HasIndex(x => new { x.TenantId, x.FleetBatteryId, x.EventAtUtc, x.IsDeleted });
        builder.HasIndex(x => new { x.TenantId, x.VehicleAssetId, x.EventAtUtc, x.IsDeleted });

        builder.Property(x => x.EventType).HasMaxLength(30);
        builder.Property(x => x.FromPosition).HasMaxLength(30);
        builder.Property(x => x.ToPosition).HasMaxLength(30);
        builder.Property(x => x.FromStatus).HasMaxLength(20);
        builder.Property(x => x.ToStatus).HasMaxLength(20);
    }
}

