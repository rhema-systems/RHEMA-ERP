using ErpSystem.Core.Entities.Maintenance;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ErpSystem.Data.Configuration.Maintenance;

public sealed class FleetIncidentConfiguration : IEntityTypeConfiguration<FleetIncident>
{
    public void Configure(EntityTypeBuilder<FleetIncident> builder)
    {
        builder.ToTable("FleetIncidents");

        builder.HasIndex(x => new { x.TenantId, x.VehicleAssetId, x.OccurredAtUtc, x.IsDeleted });
        builder.HasIndex(x => new { x.TenantId, x.Status, x.IsDeleted });

        builder.Property(x => x.IncidentType).HasMaxLength(30);
        builder.Property(x => x.Severity).HasMaxLength(20);
        builder.Property(x => x.Status).HasMaxLength(20);
        builder.Property(x => x.CurrencyCode).HasMaxLength(10);
        builder.Property(x => x.ClaimStatus).HasMaxLength(30);
    }
}

