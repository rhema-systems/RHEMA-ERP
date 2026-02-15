using ErpSystem.Core.Entities.Maintenance;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ErpSystem.Data.Configuration.Maintenance;

public sealed class FleetTyreConfiguration : IEntityTypeConfiguration<FleetTyre>
{
    public void Configure(EntityTypeBuilder<FleetTyre> builder)
    {
        builder.ToTable("FleetTyres");

        builder.HasIndex(x => new { x.TenantId, x.VehicleAssetId, x.Status, x.IsDeleted });
        builder.HasIndex(x => new { x.TenantId, x.SerialNumber, x.IsDeleted });

        builder.Property(x => x.SerialNumber).HasMaxLength(100);
        builder.Property(x => x.Status).HasMaxLength(20);
        builder.Property(x => x.Position).HasMaxLength(30);
    }
}

