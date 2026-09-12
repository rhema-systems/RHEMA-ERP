using ErpSystem.Core.Entities.Inventory;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ErpSystem.Data.Configuration;

public sealed class InventoryTransferControlRootConfiguration : IEntityTypeConfiguration<InventoryTransfer>
{
    public void Configure(EntityTypeBuilder<InventoryTransfer> builder)
    {
        builder.ToTable("InventoryTransfers", table => table.HasTrigger("TR_InventoryTransfers_ControlledLifecycle"));
        builder.Property(x => x.RowVersion).IsRowVersion();
        builder.Property(x => x.ApprovalRequired).HasDefaultValue(true);
    }
}

public sealed class InventoryTransferControlItemConfiguration : IEntityTypeConfiguration<InventoryTransferItem>
{
    public void Configure(EntityTypeBuilder<InventoryTransferItem> builder)
    {
        builder.ToTable("InventoryTransferItems", table =>
        {
            table.HasTrigger("TR_InventoryTransferItems_ControlledMutation");
            table.HasCheckConstraint("CK_InventoryTransferItems_ControlledQuantity", "[RequestedQuantity] > 0 AND [ShippedQuantity] >= 0 AND [ReceivedQuantity] >= 0 AND [DamagedQuantity] >= 0 AND [ShortageQuantity] >= 0 AND [ShippedQuantity] <= [RequestedQuantity] AND ([ReceivedQuantity] + [DamagedQuantity] + [ShortageQuantity]) <= [ShippedQuantity]");
        });
    }
}

public sealed class InventoryTransferActionConfiguration : IEntityTypeConfiguration<InventoryTransferAction>
{
    public void Configure(EntityTypeBuilder<InventoryTransferAction> builder)
    {
        builder.ToTable("InventoryTransferActions", table => table.HasTrigger("TR_InventoryTransferActions_AppendOnly"));
        builder.HasIndex(x => new { x.TenantId, x.InventoryTransferId, x.Sequence }).IsUnique();
        builder.HasIndex(x => new { x.TenantId, x.InventoryTransferId, x.ActionType, x.IdempotencyKey }).IsUnique();
        builder.HasOne(x => x.InventoryTransfer).WithMany(x => x.Actions).HasForeignKey(x => x.InventoryTransferId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ActorUser).WithMany().HasForeignKey(x => x.ActorUserId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class InventoryTransferActionLineConfiguration : IEntityTypeConfiguration<InventoryTransferActionLine>
{
    public void Configure(EntityTypeBuilder<InventoryTransferActionLine> builder)
    {
        builder.ToTable("InventoryTransferActionLines", table =>
        {
            table.HasTrigger("TR_InventoryTransferActionLines_AppendOnly");
            table.HasCheckConstraint("CK_InventoryTransferActionLines_Quantity", "[DispatchedQuantity] >= 0 AND [ReceivedQuantity] >= 0 AND [DamagedQuantity] >= 0 AND [ShortageQuantity] >= 0 AND ([DispatchedQuantity] + [ReceivedQuantity] + [DamagedQuantity] + [ShortageQuantity]) > 0");
        });
        builder.HasIndex(x => new { x.TenantId, x.InventoryTransferActionId, x.InventoryTransferItemId }).IsUnique();
        builder.HasOne(x => x.InventoryTransferAction).WithMany(x => x.Lines).HasForeignKey(x => x.InventoryTransferActionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.InventoryTransferItem).WithMany().HasForeignKey(x => x.InventoryTransferItemId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class InventoryTransferDiscrepancyConfiguration : IEntityTypeConfiguration<InventoryTransferDiscrepancy>
{
    public void Configure(EntityTypeBuilder<InventoryTransferDiscrepancy> builder)
    {
        builder.ToTable("InventoryTransferDiscrepancies", table =>
        {
            table.HasTrigger("TR_InventoryTransferDiscrepancies_ControlledLifecycle");
            table.HasCheckConstraint("CK_InventoryTransferDiscrepancies_Quantity", "[DamagedQuantity] >= 0 AND [ShortageQuantity] >= 0 AND ([DamagedQuantity] + [ShortageQuantity]) > 0");
        });
        builder.HasIndex(x => new { x.TenantId, x.InventoryTransferId, x.Status });
        builder.HasOne(x => x.InventoryTransfer).WithMany(x => x.Discrepancies).HasForeignKey(x => x.InventoryTransferId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.InventoryTransferItem).WithMany().HasForeignKey(x => x.InventoryTransferItemId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ReceiptAction).WithMany().HasForeignKey(x => x.ReceiptActionId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class InventoryTransferDiscrepancyEvidenceConfiguration : IEntityTypeConfiguration<InventoryTransferDiscrepancyEvidence>
{
    public void Configure(EntityTypeBuilder<InventoryTransferDiscrepancyEvidence> builder)
    {
        builder.ToTable("InventoryTransferDiscrepancyEvidence", table => table.HasTrigger("TR_InventoryTransferDiscrepancyEvidence_AppendOnly"));
        builder.HasIndex(x => new { x.TenantId, x.InventoryTransferDiscrepancyId, x.CentralDocumentVersionId }).IsUnique();
        builder.HasOne(x => x.InventoryTransferDiscrepancy).WithMany(x => x.Evidence).HasForeignKey(x => x.InventoryTransferDiscrepancyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.CentralDocumentVersion).WithMany().HasForeignKey(x => x.CentralDocumentVersionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.FileUploadRecord).WithMany().HasForeignKey(x => x.FileUploadRecordId).OnDelete(DeleteBehavior.Restrict);
    }
}
