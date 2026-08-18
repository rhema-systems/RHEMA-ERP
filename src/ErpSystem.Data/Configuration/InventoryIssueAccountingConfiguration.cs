using ErpSystem.Core.Entities.Inventory;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ErpSystem.Data.Configuration;

public sealed class InventoryIssueAccountingRuleConfiguration : IEntityTypeConfiguration<InventoryIssueAccountingRule>
{
    public void Configure(EntityTypeBuilder<InventoryIssueAccountingRule> builder)
    {
        builder.ToTable("InventoryIssueAccountingRules", table =>
        {
            table.HasTrigger("TR_InventoryIssueAccountingRules_TenantOwner");
            table.HasCheckConstraint("CK_InventoryIssueAccountingRules_ItemType", "[ItemType] IN (1,2,3,4)");
            table.HasCheckConstraint("CK_InventoryIssueAccountingRules_Treatment", "[Treatment] IN (1,2)");
            table.HasCheckConstraint("CK_InventoryIssueAccountingRules_Effective", "[EffectiveToUtc] IS NULL OR [EffectiveToUtc] > [EffectiveFromUtc]");
            table.HasCheckConstraint("CK_InventoryIssueAccountingRules_Owner", "([Treatment] = 1 AND [ExpenseAccountId] IS NOT NULL AND [FixedAssetCategoryId] IS NULL AND [ItemType] <> 4) OR ([Treatment] = 2 AND [ExpenseAccountId] IS NULL AND [FixedAssetCategoryId] IS NOT NULL AND [ItemType] = 4 AND [MovementReasonCode] = 'ASSET_CUSTODY')");
        });
        builder.Property(value => value.RowVersion).IsRowVersion();
        builder.HasIndex(value => new { value.TenantId, value.InventoryCategoryId, value.ItemType, value.MovementReasonCode })
            .IsUnique().HasFilter("[IsDeleted] = 0");
        builder.HasIndex(value => new { value.TenantId, value.IsActive, value.EffectiveFromUtc, value.EffectiveToUtc });
        builder.HasOne(value => value.InventoryCategory).WithMany().HasForeignKey(value => value.InventoryCategoryId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(value => value.ExpenseAccount).WithMany().HasForeignKey(value => value.ExpenseAccountId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(value => value.FixedAssetCategory).WithMany().HasForeignKey(value => value.FixedAssetCategoryId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class InventoryIssueFinanceLineageConfiguration : IEntityTypeConfiguration<InventoryIssueFinanceLineage>
{
    public void Configure(EntityTypeBuilder<InventoryIssueFinanceLineage> builder)
    {
        builder.ToTable("InventoryIssueFinanceLineages", table =>
        {
            table.HasCheckConstraint("CK_InventoryIssueFinanceLineages_Treatment", "[Treatment] IN (1,2)");
            table.HasCheckConstraint("CK_InventoryIssueFinanceLineages_Status", "[Status] IN (1,2,3)");
            table.HasCheckConstraint("CK_InventoryIssueFinanceLineages_Quantity", "[IssuedQuantity] > 0 AND [ReturnedQuantity] >= 0 AND [ReturnedQuantity] <= [IssuedQuantity]");
            table.HasCheckConstraint("CK_InventoryIssueFinanceLineages_Value", "[IssuedValue] > 0");
            table.HasCheckConstraint("CK_InventoryIssueFinanceLineages_Asset", "([Treatment] = 1 AND [FixedAssetId] IS NULL) OR ([Treatment] = 2 AND [FixedAssetId] IS NOT NULL AND [IssuedQuantity] = 1)");
            table.HasCheckConstraint("CK_InventoryIssueFinanceLineages_Integrity", "LEN([IntegrityHash]) = 64");
            table.HasTrigger("TR_InventoryIssueFinanceLineages_Lifecycle");
        });
        builder.Property(value => value.IssuedValue).HasColumnType("decimal(18,2)");
        builder.Property(value => value.RowVersion).IsRowVersion();
        builder.HasIndex(value => new { value.TenantId, value.InventoryIssueVoucherLineId }).IsUnique();
        builder.HasIndex(value => new { value.TenantId, value.FixedAssetId }).IsUnique().HasFilter("[FixedAssetId] IS NOT NULL AND [IsDeleted] = 0");
        builder.HasIndex(value => new { value.TenantId, value.PostingEventId });
        builder.HasOne(value => value.InventoryIssueVoucherLine).WithMany().HasForeignKey(value => value.InventoryIssueVoucherLineId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(value => value.InventoryIssueAccountingRule).WithMany().HasForeignKey(value => value.InventoryIssueAccountingRuleId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(value => value.FixedAsset).WithMany().HasForeignKey(value => value.FixedAssetId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class InventoryIssueReturnAllocationConfiguration : IEntityTypeConfiguration<InventoryIssueReturnAllocation>
{
    public void Configure(EntityTypeBuilder<InventoryIssueReturnAllocation> builder)
    {
        builder.ToTable("InventoryIssueReturnAllocations", table =>
        {
            table.HasCheckConstraint("CK_InventoryIssueReturnAllocations_Quantity", "[Quantity] > 0 AND [Value] > 0");
            table.HasCheckConstraint("CK_InventoryIssueReturnAllocations_Reversal", "([ReversalPostingEventId] IS NULL AND [ReversalJournalEntryId] IS NULL AND [ReversedAtUtc] IS NULL) OR ([ReversalPostingEventId] IS NOT NULL AND [ReversalJournalEntryId] IS NOT NULL AND [ReversedAtUtc] IS NOT NULL)");
            table.HasCheckConstraint("CK_InventoryIssueReturnAllocations_Integrity", "LEN([IntegrityHash]) = 64");
            table.HasTrigger("TR_InventoryIssueReturnAllocations_Immutable");
        });
        builder.Property(value => value.Value).HasColumnType("decimal(18,2)");
        builder.HasIndex(value => new { value.TenantId, value.InventoryReturnVoucherLineId, value.InventoryIssueFinanceLineageId }).IsUnique();
        builder.HasIndex(value => new { value.TenantId, value.ReturnPostingEventId });
        builder.HasIndex(value => new { value.TenantId, value.ReversalPostingEventId }).HasFilter("[ReversalPostingEventId] IS NOT NULL");
        builder.HasOne(value => value.InventoryReturnVoucherLine).WithMany().HasForeignKey(value => value.InventoryReturnVoucherLineId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(value => value.InventoryIssueFinanceLineage).WithMany().HasForeignKey(value => value.InventoryIssueFinanceLineageId).OnDelete(DeleteBehavior.Restrict);
    }
}
