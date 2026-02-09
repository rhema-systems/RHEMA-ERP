using ErpSystem.Core.Entities.Maintenance;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ErpSystem.Data.Configuration.Maintenance;

public sealed class FleetTripInspectionConfiguration : IEntityTypeConfiguration<FleetTripInspection>
{
    public void Configure(EntityTypeBuilder<FleetTripInspection> builder)
    {
        builder.ToTable("FleetTripInspections");

        builder.HasIndex(x => new { x.TenantId, x.FleetTripId, x.InspectionKind, x.IsDeleted });
        builder.HasIndex(x => new { x.TenantId, x.InspectionTemplateId, x.IsDeleted });

        builder.Property(x => x.InspectionKind).HasMaxLength(20);
        builder.Property(x => x.Status).HasMaxLength(20);
        builder.Property(x => x.OverallResult).HasMaxLength(20);
    }
}

