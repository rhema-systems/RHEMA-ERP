using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.Inventory;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Data.Configurations;

public static class InventorySupplierReturnFinanceConfiguration
{
    public static void Configure(ModelBuilder builder)
    {
        builder.Entity<SupplierDebitNote>(entity =>
        {
            entity.Property(x => x.DirectInvoiceAppliedAmount).HasColumnType("decimal(18,2)");
            entity.HasOne<PurchaseReturn>().WithMany().HasForeignKey(x => x.InventoryPurchaseReturnId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<InventorySupplierReturnAccountingGroup>().WithMany().HasForeignKey(x => x.InventorySupplierReturnAccountingGroupId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<FinancePostingEvent>().WithMany().HasForeignKey(x => x.ReturnDispatchPostingEventId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<JournalEntry>().WithMany().HasForeignKey(x => x.ReturnDispatchJournalEntryId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => new { x.TenantId, x.InventoryPurchaseReturnId }).IsUnique()
                .HasFilter("[InventoryPurchaseReturnId] IS NOT NULL AND [InventorySupplierReturnAccountingGroupId] IS NULL AND [IsDeleted] = 0")
                .HasDatabaseName("UX_SupplierDebitNotes_Tenant_InventoryReturn");
            entity.HasIndex(x => new { x.TenantId, x.InventorySupplierReturnAccountingGroupId }).IsUnique()
                .HasFilter("[InventorySupplierReturnAccountingGroupId] IS NOT NULL AND [IsDeleted] = 0")
                .HasDatabaseName("UX_SupplierDebitNotes_Tenant_InventoryReturnGroup");
            entity.ToTable("SupplierDebitNotes", t => t.HasTrigger("TR_SupplierDebitNotes_InventoryReturnCreditGuard"));
            entity.ToTable("SupplierDebitNotes", t => t.HasTrigger("TR_SupplierDebitNotes_ReturnAllocationGuard"));
        });
        builder.Entity<SupplierDebitNoteLineItem>(entity =>
        {
            entity.HasOne<PurchaseReturnItem>().WithMany().HasForeignKey(x => x.InventoryPurchaseReturnItemId).OnDelete(DeleteBehavior.Restrict);
            entity.ToTable("SupplierDebitNoteLineItems", table => table.HasTrigger("TR_SupplierDebitNoteLines_ReturnAllocationGuard"));
        });
        builder.Entity<PurchaseReturn>().ToTable("PurchaseReturns", table => table.HasTrigger("TR_PurchaseReturns_AccountingAllocationSeal"));
        builder.Entity<FinanceSettings>(entity =>
        {
            entity.HasOne<Account>().WithMany().HasForeignKey(x => x.ReturnToVendorClearingAccountId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Account>().WithMany().HasForeignKey(x => x.PurchaseReturnVarianceAccountId).OnDelete(DeleteBehavior.Restrict);
        });
        builder.Entity<InventorySupplierReturnPosting>(entity =>
        {
            entity.ToTable("InventorySupplierReturnPostings", t => t.HasTrigger("TR_InventorySupplierReturnPostings_Immutable"));
            entity.Property(x => x.CarryingAmount).HasColumnType("decimal(18,2)");
            entity.HasIndex(x => new { x.TenantId, x.InventoryPurchaseReturnId }).IsUnique();
            entity.HasOne<PurchaseReturn>().WithMany().HasForeignKey(x => x.InventoryPurchaseReturnId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<VendorInvoice>().WithMany().HasForeignKey(x => x.OriginalVendorInvoiceId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<FinancePostingEvent>().WithMany().HasForeignKey(x => x.PostingEventId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<JournalEntry>().WithMany().HasForeignKey(x => x.JournalEntryId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Account>().WithMany().HasForeignKey(x => x.ClearingAccountId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Account>().WithMany().HasForeignKey(x => x.InventoryAccountId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
