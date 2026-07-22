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
        builder.HasIndex(x => new { x.TenantId, x.VehicleAssetId, x.StartedAtUtc, x.IsDeleted });
        builder.HasIndex(x => new { x.TenantId, x.InspectionTemplateId, x.IsDeleted });
        builder.HasIndex(x => new { x.TenantId, x.ClientSubmissionId })
            .IsUnique()
            .HasFilter("[ClientSubmissionId] IS NOT NULL");

        builder.Property(x => x.InspectionKind).HasMaxLength(20);
        builder.Property(x => x.Status).HasMaxLength(20);
        builder.Property(x => x.OverallResult).HasMaxLength(20);
        builder.Property(x => x.ClientSubmissionId).HasMaxLength(100);

        builder.HasOne(x => x.FleetTrip)
            .WithMany()
            .HasForeignKey(x => x.FleetTripId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.VehicleAsset)
            .WithMany()
            .HasForeignKey(x => x.VehicleAssetId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

