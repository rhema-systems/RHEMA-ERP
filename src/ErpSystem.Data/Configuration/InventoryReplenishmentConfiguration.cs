using ErpSystem.Core.Entities.Inventory;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ErpSystem.Data.Configuration;

public sealed class InventoryReplenishmentRecommendationConfiguration : IEntityTypeConfiguration<InventoryReplenishmentRecommendation>
{
    public void Configure(EntityTypeBuilder<InventoryReplenishmentRecommendation> builder)
    {
        builder.ToTable("InventoryReplenishmentRecommendations", table =>
        {
            table.HasCheckConstraint("CK_InventoryReplenishment_Status", "[Status] BETWEEN 1 AND 7");
            table.HasCheckConstraint("CK_InventoryReplenishment_DemandWindow", "[DemandWindowDays] BETWEEN 1 AND 730 AND [DemandFromUtc] < [DemandToUtc]");
            table.HasCheckConstraint("CK_InventoryReplenishment_Quantities", "[AllocatedStock] >= 0 AND [OnOrderQuantity] >= 0 AND [OpenRecommendationQuantity] >= 0 AND [RecommendedQuantity] > 0 AND [MinimumOrderQuantity] > 0 AND [OrderMultiple] > 0");
            table.HasCheckConstraint("CK_InventoryReplenishment_Levels", "[MinimumLevel] >= 0 AND [MaximumLevel] >= 0 AND [ReorderLevel] >= 0 AND [ReorderQuantity] >= 0 AND [SafetyStock] >= 0 AND ([MaximumLevel] = 0 OR [MaximumLevel] >= [MinimumLevel])");
            table.HasCheckConstraint("CK_InventoryReplenishment_Hashes", "LEN([CalculationHash]) = 64 AND LEN([PayloadHash]) = 64 AND LEN([IdempotencyKey]) > 0 AND LEN([CorrelationId]) > 0");
            table.HasCheckConstraint("CK_InventoryReplenishment_Dates", "[RequiredDateUtc] >= [GeneratedAtUtc] AND [ValidUntilUtc] > [GeneratedAtUtc]");
        });
        builder.Property(value => value.RowVersion).IsRowVersion();
        builder.HasIndex(value => new { value.TenantId, value.RecommendationNumber }).IsUnique();
        builder.HasIndex(value => new { value.TenantId, value.IdempotencyKey }).IsUnique();
        builder.HasIndex(value => new { value.TenantId, value.WarehouseId, value.InventoryItemId, value.Status })
            .IsUnique().HasFilter("[IsDeleted] = 0 AND [Status] IN (1,2,3)");
        builder.HasIndex(value => new { value.TenantId, value.Status, value.ValidUntilUtc });
        builder.HasIndex(value => value.WorkflowInstanceId).IsUnique().HasFilter("[WorkflowInstanceId] IS NOT NULL");
        builder.HasIndex(value => value.PurchaseRequisitionId).IsUnique().HasFilter("[PurchaseRequisitionId] IS NOT NULL");
        builder.HasIndex(value => value.AlertNotificationId).IsUnique().HasFilter("[AlertNotificationId] IS NOT NULL");
        builder.HasOne(value => value.WarehouseQuantity).WithMany().HasForeignKey(value => value.WarehouseQuantityId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(value => value.Warehouse).WithMany().HasForeignKey(value => value.WarehouseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(value => value.InventoryItem).WithMany().HasForeignKey(value => value.InventoryItemId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(value => value.ItemSupplier).WithMany().HasForeignKey(value => value.ItemSupplierId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(value => value.AlertNotification).WithMany().HasForeignKey(value => value.AlertNotificationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(value => value.GeneratedBy).WithMany().HasForeignKey(value => value.GeneratedById).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(value => value.SubmittedBy).WithMany().HasForeignKey(value => value.SubmittedById).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(value => value.DecidedBy).WithMany().HasForeignKey(value => value.DecidedById).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(value => value.PurchaseRequisition).WithMany().HasForeignKey(value => value.PurchaseRequisitionId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class InventoryReplenishmentActionConfiguration : IEntityTypeConfiguration<InventoryReplenishmentAction>
{
    public void Configure(EntityTypeBuilder<InventoryReplenishmentAction> builder)
    {
        builder.ToTable("InventoryReplenishmentActions", table =>
        {
            table.HasCheckConstraint("CK_InventoryReplenishmentActions_Sequence", "[Sequence] > 0");
            table.HasCheckConstraint("CK_InventoryReplenishmentActions_Hashes", "LEN([PayloadHash]) = 64 AND LEN([IntegrityHash]) = 64 AND ([PreviousHash] IS NULL OR LEN([PreviousHash]) = 64)");
        });
        builder.HasIndex(value => new { value.TenantId, value.RecommendationId, value.Sequence }).IsUnique();
        builder.HasIndex(value => new { value.TenantId, value.RecommendationId, value.IdempotencyKey }).IsUnique();
        builder.HasOne(value => value.Recommendation).WithMany(value => value.Actions).HasForeignKey(value => value.RecommendationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(value => value.ActorUser).WithMany().HasForeignKey(value => value.ActorUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
