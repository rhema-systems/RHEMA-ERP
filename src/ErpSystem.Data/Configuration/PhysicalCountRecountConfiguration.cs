using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Inventory;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ErpSystem.Data.Configuration;

public sealed class PhysicalCountAdjustmentClaimConfiguration : IEntityTypeConfiguration<PhysicalCountAdjustmentClaim>
{
    public void Configure(EntityTypeBuilder<PhysicalCountAdjustmentClaim> builder)
    {
        builder.ToTable("PhysicalCountAdjustmentClaims", table => table.HasTrigger("TR_PhysicalCountAdjustmentClaims_Immutable"));
        builder.HasIndex(x => new { x.TenantId, x.RootPhysicalCountItemId }).IsUnique();
        builder.HasOne<PhysicalCountItem>().WithMany().HasForeignKey(x => x.RootPhysicalCountItemId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne<PhysicalCount>().WithMany().HasForeignKey(x => x.PhysicalCountId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne<PhysicalCountItem>().WithMany().HasForeignKey(x => x.PhysicalCountItemId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne<StockAdjustment>().WithMany().HasForeignKey(x => x.StockAdjustmentId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne<StockAdjustmentItem>().WithMany().HasForeignKey(x => x.StockAdjustmentItemId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.ClaimedById).OnDelete(DeleteBehavior.NoAction);
    }
}
