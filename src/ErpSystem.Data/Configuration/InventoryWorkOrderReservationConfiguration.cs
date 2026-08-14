using ErpSystem.Core.Entities.Inventory;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ErpSystem.Data.Configuration;

public sealed class InventoryLocationNegativeStockTriggerConfiguration
    : IEntityTypeConfiguration<InventoryLocation>
{
    public void Configure(EntityTypeBuilder<InventoryLocation> builder)
    {
        // The shared negative-stock owner protects this table with an enabled trigger.
        // Register it so EF uses its trigger-safe DML path instead of OUTPUT without INTO.
        builder.ToTable("InventoryLocations", table =>
            table.HasTrigger("TR_InventoryLocations_NegativeStockGuard"));
    }
}

public sealed class InventoryWorkOrderAllocationConfiguration : IEntityTypeConfiguration<InventoryAllocation>
{
    public void Configure(EntityTypeBuilder<InventoryAllocation> builder)
    {
        builder.ToTable("InventoryAllocations", table =>
        {
            // Register both database triggers so EF does not emit an OUTPUT clause against
            // this trigger-protected shared table.
            table.HasTrigger("TR_InventoryAllocations_ProjectReservationGuard");
            table.HasTrigger("TR_InventoryAllocations_WorkOrderReservationGuard");
            table.HasCheckConstraint("CK_InventoryAllocations_WorkOrderLineage",
                "[AllocationType] <> 'WorkOrder' OR [IdempotencyKey] IS NULL OR ([ReferenceId] IS NOT NULL AND [LocationId] IS NOT NULL)");
            table.HasCheckConstraint("CK_InventoryAllocations_WorkOrderHashes",
                "[AllocationType] <> 'WorkOrder' OR [IdempotencyKey] IS NULL OR (LEN([IdempotencyKey]) > 0 AND LEN([PayloadHash]) = 64 AND LEN([CorrelationId]) > 0)");
            table.HasCheckConstraint("CK_InventoryAllocations_WorkOrderQuantities",
                "[AllocationType] <> 'WorkOrder' OR ([AllocatedQuantity] > 0 AND [ConsumedQuantity] >= 0 AND [RemainingQuantity] >= 0 AND [ConsumedQuantity] + [RemainingQuantity] <= [AllocatedQuantity])");
        });
        builder.HasIndex(value => new { value.TenantId, value.IdempotencyKey },
                "IX_InventoryAllocations_TenantId_WorkOrderIdempotencyKey").IsUnique()
            .HasFilter("[AllocationType] = 'WorkOrder' AND [IdempotencyKey] IS NOT NULL");
        builder.HasIndex(value => new { value.TenantId, value.ReferenceId, value.Status, value.RequiredDate },
                "IX_InventoryAllocations_TenantId_WorkOrder_Status_RequiredDate")
            .HasFilter("[AllocationType] = 'WorkOrder'");
    }
}

public sealed class InventoryWorkOrderReservationActionConfiguration
    : IEntityTypeConfiguration<InventoryWorkOrderReservationAction>
{
    public void Configure(EntityTypeBuilder<InventoryWorkOrderReservationAction> builder)
    {
        builder.ToTable("InventoryWorkOrderReservationActions", table =>
        {
            table.HasTrigger("TR_InventoryWorkOrderReservationActions_Immutable");
            table.HasCheckConstraint("CK_InventoryWorkOrderReservationActions_Sequence", "[Sequence] > 0");
            table.HasCheckConstraint("CK_InventoryWorkOrderReservationActions_Quantity", "[Quantity] >= 0");
            table.HasCheckConstraint("CK_InventoryWorkOrderReservationActions_Hashes",
                "LEN([PayloadHash]) = 64 AND LEN([IntegrityHash]) = 64 AND ([PreviousHash] IS NULL OR LEN([PreviousHash]) = 64)");
        });
        builder.Property(value => value.Quantity).HasPrecision(18, 4);
        builder.HasIndex(value => new { value.TenantId, value.InventoryAllocationId, value.Sequence }).IsUnique();
        builder.HasIndex(value => new { value.TenantId, value.InventoryAllocationId, value.IdempotencyKey }).IsUnique();
        builder.HasIndex(value => new { value.TenantId, value.WorkOrderPartId, value.OccurredAtUtc });
        builder.HasOne(value => value.InventoryAllocation).WithMany()
            .HasForeignKey(value => value.InventoryAllocationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(value => value.WorkOrderPart).WithMany()
            .HasForeignKey(value => value.WorkOrderPartId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(value => value.ActorUser).WithMany()
            .HasForeignKey(value => value.ActorUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
