using ErpSystem.Core.Entities.Inventory;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ErpSystem.Data.Configuration;

public sealed class InventoryReturnVoucherConfiguration : IEntityTypeConfiguration<InventoryReturnVoucher>
{
    public void Configure(EntityTypeBuilder<InventoryReturnVoucher> builder)
    {
        builder.ToTable("InventoryReturnVouchers", table =>
        {
            table.HasTrigger("TR_InventoryReturnVouchers_ControlledLifecycle");
            table.HasCheckConstraint("CK_InventoryReturnVouchers_Status", "[Status] BETWEEN 1 AND 6");
            table.HasCheckConstraint("CK_InventoryReturnVouchers_TotalValue", "[TotalValue] > 0");
        });
        builder.HasIndex(x => new { x.TenantId, x.VoucherNumber }).IsUnique();
        builder.HasIndex(x => new { x.TenantId, x.IdempotencyKey }).IsUnique();
        builder.HasIndex(x => new { x.TenantId, x.InventoryRequisitionId, x.Status });
        builder.Property(x => x.RowVersion).IsRowVersion();
        builder.HasOne(x => x.InventoryRequisition).WithMany().HasForeignKey(x => x.InventoryRequisitionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Warehouse).WithMany().HasForeignKey(x => x.WarehouseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.RequestedBy).WithMany().HasForeignKey(x => x.RequestedById).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class InventoryReturnVoucherLineConfiguration : IEntityTypeConfiguration<InventoryReturnVoucherLine>
{
    public void Configure(EntityTypeBuilder<InventoryReturnVoucherLine> builder)
    {
        builder.ToTable("InventoryReturnVoucherLines", table =>
        {
            table.HasTrigger("TR_InventoryReturnVoucherLines_AppendOnly");
            table.HasCheckConstraint("CK_InventoryReturnVoucherLines_Quantity", "[Quantity] > 0");
            table.HasCheckConstraint("CK_InventoryReturnVoucherLines_Value", "[UnitCost] >= 0 AND [TotalValue] >= 0");
        });
        builder.HasIndex(x => new
            {
                x.InventoryReturnVoucherId,
                x.InventoryRequisitionItemId,
                x.LocationId,
                x.LotNumber,
                x.BatchNumber,
                x.SerialNumber
            })
            .IsUnique()
            .HasDatabaseName("UX_InventoryReturnVoucherLines_Tracking")
            .HasFilter(null);
        builder.HasOne(x => x.InventoryReturnVoucher).WithMany(x => x.Lines).HasForeignKey(x => x.InventoryReturnVoucherId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.InventoryRequisitionItem).WithMany().HasForeignKey(x => x.InventoryRequisitionItemId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.InventoryItem).WithMany().HasForeignKey(x => x.InventoryItemId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Location).WithMany().HasForeignKey(x => x.LocationId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class InventoryReturnVoucherEvidenceConfiguration : IEntityTypeConfiguration<InventoryReturnVoucherEvidence>
{
    public void Configure(EntityTypeBuilder<InventoryReturnVoucherEvidence> builder)
    {
        builder.ToTable("InventoryReturnVoucherEvidence", table => table.HasTrigger("TR_InventoryReturnVoucherEvidence_AppendOnly"));
        builder.HasIndex(x => new { x.InventoryReturnVoucherId, x.CentralDocumentVersionId }).IsUnique();
        builder.HasOne(x => x.InventoryReturnVoucher).WithMany(x => x.Evidence).HasForeignKey(x => x.InventoryReturnVoucherId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.CentralDocumentVersion).WithMany().HasForeignKey(x => x.CentralDocumentVersionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.FileUploadRecord).WithMany().HasForeignKey(x => x.FileUploadRecordId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class InventoryReturnVoucherActionConfiguration : IEntityTypeConfiguration<InventoryReturnVoucherAction>
{
    public void Configure(EntityTypeBuilder<InventoryReturnVoucherAction> builder)
    {
        builder.ToTable("InventoryReturnVoucherActions", table =>
        {
            table.HasTrigger("TR_InventoryReturnVoucherActions_AppendOnly");
            table.HasCheckConstraint("CK_InventoryReturnVoucherActions_Sequence", "[Sequence] > 0");
        });
        builder.HasIndex(x => new { x.InventoryReturnVoucherId, x.Sequence }).IsUnique();
        builder.HasIndex(x => new { x.TenantId, x.IdempotencyKey }).IsUnique();
        builder.HasOne(x => x.InventoryReturnVoucher).WithMany(x => x.Actions).HasForeignKey(x => x.InventoryReturnVoucherId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ActorUser).WithMany().HasForeignKey(x => x.ActorUserId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class StockAdjustmentEvidenceConfiguration : IEntityTypeConfiguration<StockAdjustmentEvidence>
{
    public void Configure(EntityTypeBuilder<StockAdjustmentEvidence> builder)
    {
        builder.ToTable("StockAdjustmentEvidence", table => table.HasTrigger("TR_StockAdjustmentEvidence_AppendOnly"));
        builder.HasIndex(x => new { x.StockAdjustmentId, x.CentralDocumentVersionId }).IsUnique();
        builder.HasOne(x => x.StockAdjustment).WithMany(x => x.Evidence).HasForeignKey(x => x.StockAdjustmentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.CentralDocumentVersion).WithMany().HasForeignKey(x => x.CentralDocumentVersionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.FileUploadRecord).WithMany().HasForeignKey(x => x.FileUploadRecordId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class StockAdjustmentActionConfiguration : IEntityTypeConfiguration<StockAdjustmentAction>
{
    public void Configure(EntityTypeBuilder<StockAdjustmentAction> builder)
    {
        builder.ToTable("StockAdjustmentActions", table =>
        {
            table.HasTrigger("TR_StockAdjustmentActions_AppendOnly");
            table.HasCheckConstraint("CK_StockAdjustmentActions_Sequence", "[Sequence] > 0");
        });
        builder.HasIndex(x => new { x.StockAdjustmentId, x.Sequence }).IsUnique();
        builder.HasIndex(x => new { x.TenantId, x.IdempotencyKey }).IsUnique();
        builder.HasOne(x => x.StockAdjustment).WithMany(x => x.Actions).HasForeignKey(x => x.StockAdjustmentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ActorUser).WithMany().HasForeignKey(x => x.ActorUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
