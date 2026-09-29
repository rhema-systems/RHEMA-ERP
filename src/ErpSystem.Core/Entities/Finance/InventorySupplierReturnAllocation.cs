using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ErpSystem.Core.Entities.Finance;

/// <summary>Frozen accounting-stage claim over an exact original receipt quantity.</summary>
public sealed class InventorySupplierReturnAllocation : TenantEntity
{
    public Guid InventoryPurchaseReturnId { get; set; }
    public Guid InventoryPurchaseReturnItemId { get; set; }
    public Guid GoodsReceiptNoteItemId { get; set; }
    public Guid PurchaseOrderReceiptItemId { get; set; }
    public Guid? ProcurementReceiptCostBasisId { get; set; }
    public Guid AccountingGroupId { get; set; }
    public Guid? VendorInvoiceReceiptAllocationId { get; set; }
    public Guid? OriginalVendorInvoiceId { get; set; }
    public Guid? OriginalVendorInvoiceLineItemId { get; set; }
    public Guid OriginalReceiptJournalEntryId { get; set; }
    [Column(TypeName = "decimal(18,4)")] public decimal BaseQuantity { get; set; }
    [Column(TypeName = "decimal(18,4)")] public decimal PurchaseQuantity { get; set; }
    [Column(TypeName = "decimal(18,8)")] public decimal ConversionToBase { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal OriginalAccrualAmount { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal OriginalAccrualForeignAmount { get; set; }
    [Required, MaxLength(3)] public string PurchaseCurrency { get; set; } = string.Empty;
    [Column(TypeName = "decimal(18,2)")] public decimal CarryingAmount { get; set; }
    [Required, MaxLength(3)] public string FunctionalCurrency { get; set; } = string.Empty;
    public DateTime CapturedAtUtc { get; set; }
}

/// <summary>
/// One physical return may have one uninvoiced accrual group and several original
/// invoice groups. Invoice groups are credited only through governed AP debit notes.
/// </summary>
public sealed class InventorySupplierReturnAccountingGroup : TenantEntity
{
    public Guid InventoryPurchaseReturnId { get; set; }
    public Guid? OriginalVendorInvoiceId { get; set; }
    public Guid? DispatchPostingEventId { get; set; }
    public Guid? DispatchJournalEntryId { get; set; }
    public Guid? ClearingAccountId { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal CarryingAmount { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal OriginalAccrualAmount { get; set; }
    [Required, MaxLength(3)] public string FunctionalCurrency { get; set; } = string.Empty;
    public DateTime CapturedAtUtc { get; set; }
}

/// <summary>Original posted accrual account distribution retained for a return slice.</summary>
public sealed class InventorySupplierReturnAccrualShare : TenantEntity
{
    public Guid InventorySupplierReturnAllocationId { get; set; }
    public Guid OriginalReceiptAccountTransactionId { get; set; }
    public Guid AccountId { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal Amount { get; set; }
}
