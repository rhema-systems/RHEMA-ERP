using ErpSystem.Core.Entities.Maintenance;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ErpSystem.Data.Configuration.Maintenance;

public sealed class FleetFuelTransactionConfiguration : IEntityTypeConfiguration<FleetFuelTransaction>
{
    public void Configure(EntityTypeBuilder<FleetFuelTransaction> builder)
    {
        builder.ToTable("FleetFuelTransactions");

        builder.HasIndex(x => new { x.TenantId, x.VehicleAssetId, x.FuelledAt });
        builder.HasIndex(x => new { x.TenantId, x.FleetTripId });

        builder.HasOne(x => x.VehicleAsset)
            .WithMany()
            .HasForeignKey(x => x.VehicleAssetId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.FleetTrip)
            .WithMany()
            .HasForeignKey(x => x.FleetTripId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

