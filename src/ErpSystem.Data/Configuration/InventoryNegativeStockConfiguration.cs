using ErpSystem.Core.Entities.Inventory;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ErpSystem.Data.Configuration;

public sealed class InventoryNegativeStockOverrideConfiguration : IEntityTypeConfiguration<InventoryNegativeStockOverride>
{
    public void Configure(EntityTypeBuilder<InventoryNegativeStockOverride> builder)
    {
        builder.ToTable("InventoryNegativeStockOverrides", table =>
        {
            table.HasCheckConstraint("CK_InventoryNegativeStockOverrides_Quantity", "[AuthorizedQuantity] > 0");
            table.HasCheckConstraint("CK_InventoryNegativeStockOverrides_Expiry", "[ExpiresAtUtc] > [ApprovedAtUtc]");
            table.HasCheckConstraint("CK_InventoryNegativeStockOverrides_Integrity", "LEN([IntegrityHash]) = 64 AND LEN([DecisionSnapshotHash]) = 64");
            table.HasCheckConstraint("CK_InventoryNegativeStockOverrides_Consumption", "([ConsumedAtUtc] IS NULL AND [ConsumedByUserId] IS NULL AND [ConsumedByReferenceId] IS NULL AND [ConsumptionTransactionId] IS NULL) OR ([ConsumedAtUtc] IS NOT NULL AND [ConsumedByUserId] IS NOT NULL AND [ConsumedByReferenceId] IS NOT NULL AND [ConsumptionTransactionId] IS NOT NULL)");
        });
        builder.Property(value => value.AuthorizedQuantity).HasPrecision(18, 4);
        builder.Property(value => value.RowVersion).IsRowVersion();
        builder.Property(value => value.ReferenceType).HasMaxLength(100).IsRequired();
        builder.Property(value => value.ReferenceNumber).HasMaxLength(100).IsRequired();
        builder.Property(value => value.Reason).HasMaxLength(2000).IsRequired();
        builder.Property(value => value.DecisionSnapshotHash).HasMaxLength(64).IsRequired();
        builder.Property(value => value.EvidenceReference).HasMaxLength(500).IsRequired();
        builder.Property(value => value.IntegrityHash).HasMaxLength(64).IsRequired();

        builder.HasIndex(value => new { value.TenantId, value.WorkflowInstanceId }).IsUnique();
        builder.HasIndex(value => new { value.TenantId, value.ReferenceId, value.ReferenceLineId });
        builder.HasIndex(value => new { value.TenantId, value.InventoryItemId, value.WarehouseId, value.ExpiresAtUtc });

        builder.HasOne(value => value.InventoryItem).WithMany().HasForeignKey(value => value.InventoryItemId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(value => value.Warehouse).WithMany().HasForeignKey(value => value.WarehouseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(value => value.Location).WithMany().HasForeignKey(value => value.LocationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(value => value.ConfigurationProfile).WithMany().HasForeignKey(value => value.ConfigurationProfileId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(value => value.ConfigurationDecision).WithMany().HasForeignKey(value => value.ConfigurationDecisionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(value => value.WorkflowInstance).WithMany().HasForeignKey(value => value.WorkflowInstanceId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(value => value.CentralDocumentVersion).WithMany().HasForeignKey(value => value.CentralDocumentVersionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(value => value.FileUploadRecord).WithMany().HasForeignKey(value => value.FileUploadRecordId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class InventoryNegativeStockWarehouseQuantityConfiguration : IEntityTypeConfiguration<WarehouseQuantity>
{
    public void Configure(EntityTypeBuilder<WarehouseQuantity> builder) =>
        builder.ToTable("WarehouseQuantities", table =>
            table.HasTrigger("TR_WarehouseQuantities_NegativeStockGuard"));
}

public sealed class InventoryNegativeStockItemConfiguration : IEntityTypeConfiguration<InventoryItem>
{
    public void Configure(EntityTypeBuilder<InventoryItem> builder) =>
        builder.ToTable("InventoryItems", table =>
            table.HasTrigger("TR_InventoryItems_NegativeStockGuard"));
}
