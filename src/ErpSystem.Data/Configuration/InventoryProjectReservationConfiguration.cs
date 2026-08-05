using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ErpSystem.Data.Configuration;

public sealed class InventoryProjectReservationConfiguration : IEntityTypeConfiguration<InventoryAllocation>
{
    public void Configure(EntityTypeBuilder<InventoryAllocation> builder)
    {
        builder.ToTable("InventoryAllocations", table =>
        {
            table.HasCheckConstraint("CK_InventoryAllocations_ProjectLineage",
                "[AllocationType] <> 'ProjectRequisition' OR ([InventoryRequisitionId] IS NOT NULL AND [InventoryRequisitionItemId] IS NOT NULL AND [ProjectId] IS NOT NULL AND [DepartmentId] IS NOT NULL AND [LocationId] IS NOT NULL)");
            table.HasCheckConstraint("CK_InventoryAllocations_ProjectHashes",
                "[AllocationType] <> 'ProjectRequisition' OR (LEN([IdempotencyKey]) > 0 AND LEN([PayloadHash]) = 64 AND LEN([CorrelationId]) > 0)");
            table.HasCheckConstraint("CK_InventoryAllocations_Quantities",
                "[AllocationType] <> 'ProjectRequisition' OR ([AllocatedQuantity] > 0 AND [ConsumedQuantity] >= 0 AND [RemainingQuantity] >= 0 AND [ConsumedQuantity] + [RemainingQuantity] <= [AllocatedQuantity])");
        });
        builder.Property(value => value.RowVersion).IsRowVersion();
        // Preserve the existing tenant lookup index used by every allocation owner.
        // The project-specific composites supplement it; they do not replace it.
        builder.HasIndex(value => value.TenantId);
        builder.HasIndex(value => new { value.TenantId, value.IdempotencyKey }).IsUnique()
            .HasFilter("[AllocationType] = 'ProjectRequisition' AND [IdempotencyKey] IS NOT NULL");
        builder.HasIndex(value => new { value.TenantId, value.ProjectId, value.Status, value.ExpirationDate });
        builder.HasIndex(value => new { value.TenantId, value.DepartmentId, value.Status, value.ExpirationDate });
        builder.HasIndex(value => new { value.TenantId, value.InventoryRequisitionItemId, value.Status })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0 AND [AllocationType] = 'ProjectRequisition' AND [Status] IN ('Active','PartiallyFulfilled')");

        builder.HasOne(value => value.InventoryRequisition).WithMany()
            .HasForeignKey(value => value.InventoryRequisitionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(value => value.InventoryRequisitionItem).WithMany()
            .HasForeignKey(value => value.InventoryRequisitionItemId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(value => value.Project).WithMany()
            .HasForeignKey(value => value.ProjectId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(value => value.SubstitutedFromAllocation).WithOne(value => value.SubstitutedByAllocation)
            .HasForeignKey<InventoryAllocation>(value => value.SubstitutedFromAllocationId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class InventoryProjectReservationActionConfiguration : IEntityTypeConfiguration<InventoryProjectReservationAction>
{
    public void Configure(EntityTypeBuilder<InventoryProjectReservationAction> builder)
    {
        builder.ToTable("InventoryProjectReservationActions", table =>
        {
            table.HasCheckConstraint("CK_InventoryProjectReservationActions_Sequence", "[Sequence] > 0");
            table.HasCheckConstraint("CK_InventoryProjectReservationActions_Quantity", "[Quantity] >= 0");
            table.HasCheckConstraint("CK_InventoryProjectReservationActions_Hashes",
                "LEN([PayloadHash]) = 64 AND LEN([IntegrityHash]) = 64 AND ([PreviousHash] IS NULL OR LEN([PreviousHash]) = 64)");
        });
        builder.Property(value => value.Quantity).HasPrecision(18, 4);
        builder.HasIndex(value => new { value.TenantId, value.InventoryAllocationId, value.Sequence }).IsUnique();
        builder.HasIndex(value => new { value.TenantId, value.InventoryAllocationId, value.IdempotencyKey }).IsUnique();
        builder.HasIndex(value => value.NotificationId).IsUnique().HasFilter("[NotificationId] IS NOT NULL");
        builder.HasOne(value => value.InventoryAllocation).WithMany(value => value.ProjectReservationActions)
            .HasForeignKey(value => value.InventoryAllocationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(value => value.ActorUser).WithMany()
            .HasForeignKey(value => value.ActorUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(value => value.Notification).WithMany()
            .HasForeignKey(value => value.NotificationId).OnDelete(DeleteBehavior.Restrict);
    }
}
