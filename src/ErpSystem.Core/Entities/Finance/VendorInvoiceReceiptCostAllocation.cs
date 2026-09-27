using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ErpSystem.Core.Entities.Finance;

/// <summary>Immutable original receipt clearing and invoice-price policy evidence for an exact receipt quantity share.</summary>
public sealed class VendorInvoiceReceiptCostAllocation : TenantEntity
{
    public Guid VendorInvoiceId { get; set; }
    public Guid VendorInvoiceLineItemId { get; set; }
    public Guid VendorInvoiceReceiptAllocationId { get; set; }
    public Guid? ProcurementReceiptCostBasisId { get; set; }
    public Guid GoodsReceiptNoteItemId { get; set; }
    public Guid InventoryItemId { get; set; }
    public Guid ReceiptJournalEntryId { get; set; }
    public Guid AccountingBookId { get; set; }
    [Column(TypeName = "decimal(18,4)")] public decimal PurchaseQuantity { get; set; }
    [Column(TypeName = "decimal(18,4)")] public decimal BaseQuantity { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal ReceiptForeignAmount { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal ReceiptFunctionalAmount { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal InvoiceNetForeignAmount { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal InvoiceFunctionalAmount { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal PriceDifferenceFunctionalAmount { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal ExchangeDifferenceFunctionalAmount { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal InventoryAdjustmentAmount { get; set; }
    [Column(TypeName = "decimal(28,12)")] public decimal RevaluedReceiptBaseQuantity { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal PurchasePriceVarianceAmount { get; set; }
    [Required, MaxLength(30)] public string Policy { get; set; } = "NoDifference";
    [Required, MaxLength(3)] public string PurchaseCurrency { get; set; } = string.Empty;
    [Required, MaxLength(3)] public string FunctionalCurrency { get; set; } = string.Empty;
    [Column(TypeName = "decimal(18,6)")] public decimal InvoiceExchangeRateToFunctional { get; set; }
    public Guid? PurchasePriceVarianceAccountId { get; set; }
    public Guid PostingEventId { get; set; }
    public Guid JournalEntryId { get; set; }
    public Guid? ReversalPostingEventId { get; set; }
    public Guid? ReversalJournalEntryId { get; set; }
    public Guid? ReversalReclassificationPostingEventId { get; set; }
    public Guid? ReversalReclassificationJournalEntryId { get; set; }
    [Required, MaxLength(64)] public string SourceFingerprint { get; set; } = string.Empty;
}

/// <summary>Signed debit amounts for each retained original-accrual, revaluation, PPV or FX purpose.</summary>
public sealed class VendorInvoiceReceiptCostPostingLine : TenantEntity
{
    public Guid CostAllocationId { get; set; }
    public Guid AccountId { get; set; }
    public Guid? OriginalReceiptAccountTransactionId { get; set; }
    [Required, MaxLength(20)] public string Purpose { get; set; } = string.Empty;
    [Column(TypeName = "decimal(18,2)")] public decimal ForeignAmount { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal FunctionalAmount { get; set; }
    public VendorInvoiceReceiptCostAllocation CostAllocation { get; set; } = null!;
}

/// <summary>Value-only movement targets; retained quantity is evidence, never a second stock receipt.</summary>
public sealed class VendorInvoiceReceiptCostValuation : TenantEntity
{
    public Guid CostAllocationId { get; set; }
    public Guid WarehouseId { get; set; }
    public Guid LocationId { get; set; }
    public Guid? InventoryLayerId { get; set; }
    public Guid InventoryMovementId { get; set; }
    [Column(TypeName = "decimal(28,12)")] public decimal AttributedReceiptBaseQuantity { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal ValueChange { get; set; }
    public Guid? ReversesValuationId { get; set; }
    public bool IsReversal { get; set; }
    public VendorInvoiceReceiptCostAllocation CostAllocation { get; set; } = null!;
}
