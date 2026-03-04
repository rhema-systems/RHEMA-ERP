using ErpSystem.Core.Entities.Maintenance;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ErpSystem.Data.Configuration.Maintenance;

public sealed class FleetTripDestinationConfiguration : IEntityTypeConfiguration<FleetTripDestination>
{
    public void Configure(EntityTypeBuilder<FleetTripDestination> builder)
    {
        builder.ToTable("FleetTripDestinations");

        builder.HasIndex(x => new { x.TenantId, x.Name });
        builder.HasIndex(x => new { x.TenantId, x.IsActive });

        builder.Property(x => x.Name).IsRequired().HasMaxLength(200);
        builder.Property(x => x.Origin).HasMaxLength(200);
        builder.Property(x => x.Destination).HasMaxLength(200);
    }
}

