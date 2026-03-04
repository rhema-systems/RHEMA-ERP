using ErpSystem.Core.Entities.Maintenance;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ErpSystem.Data.Configuration.Maintenance;

public sealed class FleetTripConfiguration : IEntityTypeConfiguration<FleetTrip>
{
    public void Configure(EntityTypeBuilder<FleetTrip> builder)
    {
        builder.ToTable("FleetTrips");

        builder.HasIndex(x => new { x.TenantId, x.VehicleAssetId });
        builder.HasIndex(x => new { x.TenantId, x.Status });
        builder.HasIndex(x => new { x.TenantId, x.RequestedByUserId });
        builder.HasIndex(x => new { x.TenantId, x.DriverEmployeeId });
        builder.HasIndex(x => new { x.TenantId, x.FleetTripDestinationId });

        builder.HasOne(x => x.VehicleAsset)
            .WithMany()
            .HasForeignKey(x => x.VehicleAssetId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.DriverEmployee)
            .WithMany()
            .HasForeignKey(x => x.DriverEmployeeId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(x => x.FleetTripDestination)
            .WithMany()
            .HasForeignKey(x => x.FleetTripDestinationId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

