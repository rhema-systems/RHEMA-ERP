using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Entities.Procurement;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ErpSystem.Data.Configurations;

public sealed class InventorySupplierReturnAllocationConfiguration : IEntityTypeConfiguration<InventorySupplierReturnAllocation>
{
    public void Configure(EntityTypeBuilder<InventorySupplierReturnAllocation> builder)
    {
        builder.ToTable("InventorySupplierReturnAllocations", table =>
        {
            table.HasTrigger("TR_InventorySupplierReturnAllocations_Authority");
            table.HasCheckConstraint("CK_InventorySupplierReturnAllocations_Quantity", "[BaseQuantity] > 0 AND [PurchaseQuantity] > 0 AND [ConversionToBase] > 0");
            table.HasCheckConstraint("CK_InventorySupplierReturnAllocations_Stage",
                "([VendorInvoiceReceiptAllocationId] IS NULL AND [OriginalVendorInvoiceId] IS NULL AND [OriginalVendorInvoiceLineItemId] IS NULL) OR " +
                "([VendorInvoiceReceiptAllocationId] IS NOT NULL AND [OriginalVendorInvoiceId] IS NOT NULL AND [OriginalVendorInvoiceLineItemId] IS NOT NULL)");
        });
        builder.HasIndex(x => new { x.TenantId, x.InventoryPurchaseReturnItemId, x.VendorInvoiceReceiptAllocationId })
            .IsUnique().HasFilter(null);
        builder.HasOne<PurchaseReturn>().WithMany().HasForeignKey(x => x.InventoryPurchaseReturnId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<PurchaseReturnItem>().WithMany().HasForeignKey(x => x.InventoryPurchaseReturnItemId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<GoodsReceiptNoteItem>().WithMany().HasForeignKey(x => x.GoodsReceiptNoteItemId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<PurchaseOrderReceiptItem>().WithMany().HasForeignKey(x => x.PurchaseOrderReceiptItemId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ProcurementReceiptCostBasis>().WithMany().HasForeignKey(x => x.ProcurementReceiptCostBasisId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<InventorySupplierReturnAccountingGroup>().WithMany().HasForeignKey(x => x.AccountingGroupId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<VendorInvoiceReceiptAllocation>().WithMany().HasForeignKey(x => x.VendorInvoiceReceiptAllocationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<VendorInvoice>().WithMany().HasForeignKey(x => x.OriginalVendorInvoiceId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<VendorInvoiceLineItem>().WithMany().HasForeignKey(x => x.OriginalVendorInvoiceLineItemId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<JournalEntry>().WithMany().HasForeignKey(x => x.OriginalReceiptJournalEntryId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class InventorySupplierReturnAccountingGroupConfiguration : IEntityTypeConfiguration<InventorySupplierReturnAccountingGroup>
{
    public void Configure(EntityTypeBuilder<InventorySupplierReturnAccountingGroup> builder)
    {
        builder.ToTable("InventorySupplierReturnAccountingGroups", table => table.HasTrigger("TR_InventorySupplierReturnAccountingGroups_Authority"));
        builder.HasIndex(x => new { x.TenantId, x.InventoryPurchaseReturnId, x.OriginalVendorInvoiceId }).IsUnique().HasFilter(null);
        builder.HasOne<PurchaseReturn>().WithMany().HasForeignKey(x => x.InventoryPurchaseReturnId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<VendorInvoice>().WithMany().HasForeignKey(x => x.OriginalVendorInvoiceId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<FinancePostingEvent>().WithMany().HasForeignKey(x => x.DispatchPostingEventId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<JournalEntry>().WithMany().HasForeignKey(x => x.DispatchJournalEntryId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Account>().WithMany().HasForeignKey(x => x.ClearingAccountId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class InventorySupplierReturnAccrualShareConfiguration : IEntityTypeConfiguration<InventorySupplierReturnAccrualShare>
{
    public void Configure(EntityTypeBuilder<InventorySupplierReturnAccrualShare> builder)
    {
        builder.ToTable("InventorySupplierReturnAccrualShares", table => table.HasTrigger("TR_InventorySupplierReturnAccrualShares_Authority"));
        builder.HasIndex(x => new { x.TenantId, x.InventorySupplierReturnAllocationId, x.OriginalReceiptAccountTransactionId }).IsUnique();
        builder.HasOne<InventorySupplierReturnAllocation>().WithMany().HasForeignKey(x => x.InventorySupplierReturnAllocationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<AccountTransaction>().WithMany().HasForeignKey(x => x.OriginalReceiptAccountTransactionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Account>().WithMany().HasForeignKey(x => x.AccountId).OnDelete(DeleteBehavior.Restrict);
    }
}
