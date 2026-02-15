using ErpSystem.Core.Entities.Maintenance;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ErpSystem.Data.Configuration.Maintenance;

public sealed class FleetDefectConfiguration : IEntityTypeConfiguration<FleetDefect>
{
    public void Configure(EntityTypeBuilder<FleetDefect> builder)
    {
        builder.ToTable("FleetDefects");

        builder.HasIndex(x => new { x.TenantId, x.VehicleAssetId, x.Status, x.IsDeleted });
        builder.HasIndex(x => new { x.TenantId, x.FleetTripId, x.IsDeleted });

        builder.Property(x => x.Severity).HasMaxLength(20);
        builder.Property(x => x.Status).HasMaxLength(20);
    }
}

