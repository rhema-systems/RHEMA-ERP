using ErpSystem.Core.Entities.Inventory;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ErpSystem.Data.Configuration;

public sealed class InventoryOptionalApprovalConfiguration :
    IEntityTypeConfiguration<PhysicalCount>, IEntityTypeConfiguration<StockAdjustment>, IEntityTypeConfiguration<InventoryReturnVoucher>
{
    public void Configure(EntityTypeBuilder<PhysicalCount> builder) => builder.Property(x => x.ApprovalRequired).HasDefaultValue(true);
    public void Configure(EntityTypeBuilder<StockAdjustment> builder) => builder.Property(x => x.ApprovalRequired).HasDefaultValue(true);
    public void Configure(EntityTypeBuilder<InventoryReturnVoucher> builder) => builder.Property(x => x.ApprovalRequired).HasDefaultValue(true);
}
