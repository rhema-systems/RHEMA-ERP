using ErpSystem.Core.Entities.Maintenance;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ErpSystem.Data.Configuration.Maintenance;

public sealed class FleetCostEntryConfiguration : IEntityTypeConfiguration<FleetCostEntry>
{
    public void Configure(EntityTypeBuilder<FleetCostEntry> builder)
    {
        builder.ToTable("FleetCostEntries");

        builder.HasIndex(x => new { x.TenantId, x.VehicleAssetId, x.CostDateUtc, x.IsDeleted });
        builder.HasIndex(x => new { x.TenantId, x.CostType, x.IsDeleted });

        builder.Property(x => x.CostType).HasMaxLength(50);
        builder.Property(x => x.Source).HasMaxLength(50).HasDefaultValue("Manual");
        builder.Property(x => x.CurrencyCode).HasMaxLength(10);

        builder.HasOne(x => x.FleetIncident)
            .WithMany()
            .HasForeignKey(x => x.FleetIncidentId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

