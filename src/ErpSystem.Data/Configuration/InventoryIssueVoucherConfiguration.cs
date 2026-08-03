using ErpSystem.Core.Entities.Inventory;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ErpSystem.Data.Configuration;

public sealed class InventoryIssueVoucherConfiguration : IEntityTypeConfiguration<InventoryIssueVoucher>
{
    public void Configure(EntityTypeBuilder<InventoryIssueVoucher> builder)
    {
        builder.ToTable("InventoryIssueVouchers", table =>
        {
            table.HasCheckConstraint("CK_InventoryIssueVouchers_Status", "[Status] IN (1,2)");
            table.HasCheckConstraint("CK_InventoryIssueVouchers_Hashes", "LEN([PayloadHash]) = 64 AND LEN([IntegrityHash]) = 64");
            table.HasCheckConstraint("CK_InventoryIssueVouchers_Sod", "[RequestedById] <> [ApprovedById] AND [RequestedById] <> [IssuedById] AND [ApprovedById] <> [IssuedById] AND [IssuedById] <> [ReceiverUserId]");
            table.HasCheckConstraint("CK_InventoryIssueVouchers_Acknowledgement", "([Status] = 1 AND [AcknowledgedById] IS NULL AND [AcknowledgedAtUtc] IS NULL) OR ([Status] = 2 AND [AcknowledgedById] = [ReceiverUserId] AND [AcknowledgedAtUtc] IS NOT NULL)");
        });
        builder.Property(item => item.RowVersion).IsRowVersion();
        builder.HasIndex(item => new { item.TenantId, item.VoucherNumber }).IsUnique();
        builder.HasIndex(item => new { item.TenantId, item.InventoryRequisitionId, item.IdempotencyKey }).IsUnique();
        builder.HasIndex(item => new { item.TenantId, item.InventoryRequisitionId, item.IssuedAtUtc });
        builder.HasIndex(item => new { item.TenantId, item.ReceiverUserId, item.Status });
        builder.HasOne(item => item.InventoryRequisition).WithMany().HasForeignKey(item => item.InventoryRequisitionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.Warehouse).WithMany().HasForeignKey(item => item.WarehouseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.Location).WithMany().HasForeignKey(item => item.LocationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.RequestedBy).WithMany().HasForeignKey(item => item.RequestedById).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.ApprovedBy).WithMany().HasForeignKey(item => item.ApprovedById).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.IssuedBy).WithMany().HasForeignKey(item => item.IssuedById).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.ReceiverUser).WithMany().HasForeignKey(item => item.ReceiverUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.AcknowledgedBy).WithMany().HasForeignKey(item => item.AcknowledgedById).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class InventoryIssueVoucherLineConfiguration : IEntityTypeConfiguration<InventoryIssueVoucherLine>
{
    public void Configure(EntityTypeBuilder<InventoryIssueVoucherLine> builder)
    {
        builder.ToTable("InventoryIssueVoucherLines", table =>
        {
            table.HasCheckConstraint("CK_InventoryIssueVoucherLines_Quantity", "[Quantity] > 0");
            table.HasCheckConstraint("CK_InventoryIssueVoucherLines_Value", "[UnitCost] >= 0 AND [TotalValue] >= 0");
            table.HasCheckConstraint("CK_InventoryIssueVoucherLines_IntegrityHash", "LEN([IntegrityHash]) = 64");
        });
        builder.Property(item => item.UnitCost).HasColumnType("decimal(18,4)");
        builder.Property(item => item.TotalValue).HasColumnType("decimal(18,2)");
        builder.HasIndex(item => new { item.TenantId, item.InventoryIssueVoucherId, item.InventoryRequisitionItemId }).IsUnique();
        builder.HasIndex(item => new { item.TenantId, item.InventoryItemId, item.WarehouseId, item.LocationId });
        builder.HasOne(item => item.InventoryIssueVoucher).WithMany(item => item.Lines).HasForeignKey(item => item.InventoryIssueVoucherId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(item => item.InventoryRequisitionItem).WithMany().HasForeignKey(item => item.InventoryRequisitionItemId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.InventoryItem).WithMany().HasForeignKey(item => item.InventoryItemId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.Warehouse).WithMany().HasForeignKey(item => item.WarehouseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.Location).WithMany().HasForeignKey(item => item.LocationId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class InventoryIssueVoucherActionConfiguration : IEntityTypeConfiguration<InventoryIssueVoucherAction>
{
    public void Configure(EntityTypeBuilder<InventoryIssueVoucherAction> builder)
    {
        builder.ToTable("InventoryIssueVoucherActions", table =>
        {
            table.HasCheckConstraint("CK_InventoryIssueVoucherActions_Sequence", "[Sequence] > 0");
            table.HasCheckConstraint("CK_InventoryIssueVoucherActions_ActionType", "[ActionType] IN (1,2)");
            table.HasCheckConstraint("CK_InventoryIssueVoucherActions_StatusAfter", "[StatusAfter] IN (1,2)");
            table.HasCheckConstraint("CK_InventoryIssueVoucherActions_IntegrityHash", "LEN([IntegrityHash]) = 64");
        });
        builder.HasIndex(item => new { item.TenantId, item.InventoryIssueVoucherId, item.Sequence }).IsUnique();
        builder.HasIndex(item => new { item.TenantId, item.OccurredAtUtc });
        builder.HasOne(item => item.InventoryIssueVoucher).WithMany(item => item.Actions).HasForeignKey(item => item.InventoryIssueVoucherId).OnDelete(DeleteBehavior.Cascade);
    }
}
