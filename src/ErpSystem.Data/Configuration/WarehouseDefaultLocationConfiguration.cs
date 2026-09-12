using ErpSystem.Core.Entities.Inventory;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ErpSystem.Data.Configuration;

public sealed class WarehouseDefaultLocationConfiguration : IEntityTypeConfiguration<WarehouseLocation>
{
    public void Configure(EntityTypeBuilder<WarehouseLocation> builder)
    {
        builder.Property(x => x.IsDefault).HasDefaultValue(false);
        builder.HasIndex(x => new { x.TenantId, x.WarehouseId })
            .HasDatabaseName("UX_WarehouseLocations_Default")
            .IsUnique().HasFilter("[IsDefault] = 1 AND [IsActive] = 1 AND [IsDeleted] = 0");
    }
}
