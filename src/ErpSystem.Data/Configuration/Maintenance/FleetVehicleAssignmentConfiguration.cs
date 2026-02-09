using ErpSystem.Core.Entities.Maintenance;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ErpSystem.Data.Configuration.Maintenance;

public sealed class FleetVehicleAssignmentConfiguration : IEntityTypeConfiguration<FleetVehicleAssignment>
{
    public void Configure(EntityTypeBuilder<FleetVehicleAssignment> builder)
    {
        builder.ToTable("FleetVehicleAssignments");

        builder.HasIndex(x => new { x.TenantId, x.VehicleAssetId, x.IsActive });
        builder.HasIndex(x => new { x.TenantId, x.EmployeeId, x.IsActive });

        builder.Property(x => x.AssignmentType).HasMaxLength(20);
    }
}

