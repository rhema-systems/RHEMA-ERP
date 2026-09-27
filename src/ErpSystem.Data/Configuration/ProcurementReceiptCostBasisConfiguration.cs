using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Entities.Procurement;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ErpSystem.Data.Configuration;

public sealed class ProcurementReceiptCostBasisConfiguration : IEntityTypeConfiguration<ProcurementReceiptCostBasis>
{
    public void Configure(EntityTypeBuilder<ProcurementReceiptCostBasis> builder)
    {
        builder.ToTable("ProcurementReceiptCostBases", table => {
            table.HasTrigger("TR_ProcurementReceiptCostBases_Guard");
            table.HasCheckConstraint("CK_ProcurementReceiptCostBases_Quantity", "[PurchaseQuantity]>0 AND [BaseQuantity]>0 AND [ConversionToBase]>0 AND [ExchangeRateToFunctional]>0 AND [PurchaseAmount]>=0 AND [FunctionalAccrualAmount]>=0 AND [FunctionalInventoryAmount]>=0");
        });
        builder.HasIndex(x => new { x.TenantId, x.PurchaseOrderReceiptItemId }).IsUnique();
        builder.HasIndex(x => new { x.TenantId, x.InventoryMovementId }).IsUnique();
        builder.HasOne<PurchaseOrderReceipt>().WithMany().HasForeignKey(x => x.PurchaseOrderReceiptId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<PurchaseOrderReceiptItem>().WithMany().HasForeignKey(x => x.PurchaseOrderReceiptItemId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<PurchaseOrderItem>().WithMany().HasForeignKey(x => x.PurchaseOrderItemId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<InventoryItem>().WithMany().HasForeignKey(x => x.InventoryItemId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<InventoryMovement>().WithMany().HasForeignKey(x => x.InventoryMovementId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Warehouse>().WithMany().HasForeignKey(x => x.WarehouseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<WarehouseLocation>().WithMany().HasForeignKey(x => x.LocationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ExchangeRate>().WithMany().HasForeignKey(x => x.ExchangeRateId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class VendorInvoiceReceiptCostAllocationConfiguration : IEntityTypeConfiguration<VendorInvoiceReceiptCostAllocation>
{
    public void Configure(EntityTypeBuilder<VendorInvoiceReceiptCostAllocation> builder)
    {
        builder.ToTable("VendorInvoiceReceiptCostAllocations", table => {
            table.HasTrigger("TR_VendorInvoiceReceiptCostAllocations_Guard");
            table.HasCheckConstraint("CK_VendorInvoiceReceiptCostAllocations_Conservation", "[PurchaseQuantity]>0 AND [BaseQuantity]>0 AND [ReceiptFunctionalAmount]+[ExchangeDifferenceFunctionalAmount]+[InventoryAdjustmentAmount]+[PurchasePriceVarianceAmount]=[InvoiceFunctionalAmount] AND [InventoryAdjustmentAmount]+[PurchasePriceVarianceAmount]=[PriceDifferenceFunctionalAmount]");
        });
        builder.HasIndex(x => new { x.TenantId, x.VendorInvoiceReceiptAllocationId }).IsUnique();
        builder.HasIndex(x => new { x.TenantId, x.GoodsReceiptNoteItemId });
        builder.HasOne<VendorInvoice>().WithMany().HasForeignKey(x => x.VendorInvoiceId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<VendorInvoiceLineItem>().WithMany().HasForeignKey(x => x.VendorInvoiceLineItemId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<VendorInvoiceReceiptAllocation>().WithMany().HasForeignKey(x => x.VendorInvoiceReceiptAllocationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ProcurementReceiptCostBasis>().WithMany().HasForeignKey(x => x.ProcurementReceiptCostBasisId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<GoodsReceiptNoteItem>().WithMany().HasForeignKey(x => x.GoodsReceiptNoteItemId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<InventoryItem>().WithMany().HasForeignKey(x => x.InventoryItemId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<JournalEntry>().WithMany().HasForeignKey(x => x.ReceiptJournalEntryId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<AccountingBook>().WithMany().HasForeignKey(x => x.AccountingBookId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Account>().WithMany().HasForeignKey(x => x.PurchasePriceVarianceAccountId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<FinancePostingEvent>().WithMany().HasForeignKey(x => x.PostingEventId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<JournalEntry>().WithMany().HasForeignKey(x => x.JournalEntryId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<FinancePostingEvent>().WithMany().HasForeignKey(x => x.ReversalPostingEventId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<JournalEntry>().WithMany().HasForeignKey(x => x.ReversalJournalEntryId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<FinancePostingEvent>().WithMany().HasForeignKey(x => x.ReversalReclassificationPostingEventId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<JournalEntry>().WithMany().HasForeignKey(x => x.ReversalReclassificationJournalEntryId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class VendorInvoiceReceiptCostPostingLineConfiguration : IEntityTypeConfiguration<VendorInvoiceReceiptCostPostingLine>
{
    public void Configure(EntityTypeBuilder<VendorInvoiceReceiptCostPostingLine> builder)
    {
        builder.ToTable("VendorInvoiceReceiptCostPostingLines", table => table.HasTrigger("TR_VendorInvoiceReceiptCostPostingLines_Guard"));
        builder.HasOne(x => x.CostAllocation).WithMany().HasForeignKey(x => x.CostAllocationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Account>().WithMany().HasForeignKey(x => x.AccountId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<AccountTransaction>().WithMany().HasForeignKey(x => x.OriginalReceiptAccountTransactionId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class VendorInvoiceReceiptCostValuationConfiguration : IEntityTypeConfiguration<VendorInvoiceReceiptCostValuation>
{
    public void Configure(EntityTypeBuilder<VendorInvoiceReceiptCostValuation> builder)
    {
        builder.ToTable("VendorInvoiceReceiptCostValuations", table => table.HasTrigger("TR_VendorInvoiceReceiptCostValuations_Guard"));
        builder.HasIndex(x => new { x.TenantId, x.InventoryMovementId }).IsUnique();
        builder.HasIndex(x => new { x.TenantId, x.ReversesValuationId }).IsUnique().HasFilter("[ReversesValuationId] IS NOT NULL");
        builder.HasOne(x => x.CostAllocation).WithMany().HasForeignKey(x => x.CostAllocationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Warehouse>().WithMany().HasForeignKey(x => x.WarehouseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<WarehouseLocation>().WithMany().HasForeignKey(x => x.LocationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<InventoryLayer>().WithMany().HasForeignKey(x => x.InventoryLayerId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<InventoryMovement>().WithMany().HasForeignKey(x => x.InventoryMovementId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<VendorInvoiceReceiptCostValuation>().WithMany().HasForeignKey(x => x.ReversesValuationId).OnDelete(DeleteBehavior.Restrict);
    }
}
