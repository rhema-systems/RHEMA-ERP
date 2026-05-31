using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Entities.Inventory;

namespace ErpSystem.Core.Entities.Finance;

#region Enums

/// <summary>
/// Status of a vendor/supplier invoice through its lifecycle.
/// </summary>
public enum VendorInvoiceStatus
{
    Draft = 1,
    PendingApproval = 2,
    Approved = 3,
    PartiallyPaid = 4,
    Paid = 5,
    Overdue = 6,
    Voided = 7,
    Rejected = 8,
    OnHold = 9
}

/// <summary>
/// Matching type selected for a vendor invoice.
/// </summary>
public enum InvoiceMatchingType
{
    None = 0,
    TwoWay = 1,   // Invoice vs Purchase Order
    ThreeWay = 2  // Invoice vs PO vs Goods Receipt
}

/// <summary>
/// Result of the matching verification process.
/// </summary>
public enum InvoiceMatchingStatus
{
    Unmatched = 0,
    TwoWayMatched = 1,
    ThreeWayMatched = 2,
    MatchException = 3
}

/// <summary>
/// Status of a vendor payment.
/// </summary>
public enum VendorPaymentStatus
{
    Draft = 1,
    PendingAuthorization = 2,
    Authorized = 3,
    Processed = 4,
    Cleared = 5,
    Voided = 6,
    Failed = 7,
    Reconciled = 8
}

/// <summary>
/// Method used to pay a vendor.
/// </summary>
public enum VendorPaymentMethod
{
    BankTransfer = 1,
    Cheque = 2,
    Cash = 3,
    WireTransfer = 4,
    MobileMoney = 5,
    DirectDebit = 6,
    Other = 99
}

/// <summary>
/// Status of a payment batch.
/// </summary>
public enum PaymentBatchStatus
{
    Draft = 1,
    PendingApproval = 2,
    Approved = 3,
    Processing = 4,
    Completed = 5,
    PartiallyCompleted = 6,
    Cancelled = 7
}

#endregion

#region Vendor Invoice

/// <summary>
/// Represents a supplier/vendor invoice in the Accounts Payable module.
/// Links to the Procurement Supplier entity and optionally to a Purchase Order for matching.
/// </summary>
public class VendorInvoice : TenantEntity
{
    // ── Identification ──────────────────────────────────────────────────

    [Required]
    [MaxLength(50)]
    public string InvoiceNumber { get; set; } = string.Empty;

    /// <summary>
    /// The supplier's own invoice/reference number printed on the physical invoice.
    /// </summary>
    [MaxLength(100)]
    public string? SupplierInvoiceNumber { get; set; }

    // ── Supplier ────────────────────────────────────────────────────────

    [Required]
    public Guid SupplierId { get; set; }
    public virtual Supplier Supplier { get; set; } = null!;

    [Required]
    [MaxLength(200)]
    public string SupplierName { get; set; } = string.Empty;

    // ── Purchase Order Link (for matching) ──────────────────────────────

    public Guid? PurchaseOrderId { get; set; }
    public virtual PurchaseOrder? PurchaseOrder { get; set; }

    // ── Dates ───────────────────────────────────────────────────────────

    [Required]
    public DateTime InvoiceDate { get; set; }

    public DateTime? ReceivedDate { get; set; }

    public DateTime? DueDate { get; set; }

    // ── Financial ───────────────────────────────────────────────────────

    [Column(TypeName = "decimal(18,2)")]
    public decimal SubTotal { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal TaxAmount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal DiscountAmount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalAmount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal PaidAmount { get; set; }

    [NotMapped]
    public decimal BalanceAmount => TotalAmount - PaidAmount;

    // ── Currency ────────────────────────────────────────────────────────

    [Required]
    [MaxLength(3)]
    public string CurrencyCode { get; set; } = "USD";

    [Column(TypeName = "decimal(18,6)")]
    public decimal ExchangeRate { get; set; } = 1.0m;

    [Column(TypeName = "decimal(18,2)")]
    public decimal BaseCurrencyAmount { get; set; }

    // ── Payment Terms ───────────────────────────────────────────────────

    public int PaymentTermsDays { get; set; } = 30;

    public Guid? PaymentTermId { get; set; }
    public virtual PaymentTerm? PaymentTerm { get; set; }

    // ── Early-Payment Discount ──────────────────────────────────────────

    [Column(TypeName = "decimal(5,2)")]
    public decimal EarlyPaymentDiscountPercentage { get; set; }

    public DateTime? EarlyPaymentDiscountDueDate { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal EarlyPaymentDiscountAmount { get; set; }

    // ── Withholding Tax ─────────────────────────────────────────────────

    [Column(TypeName = "decimal(5,2)")]
    public decimal WithholdingTaxRate { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal WithholdingTaxAmount { get; set; }

    // ── Taxation ────────────────────────────────────────────────────────
    public Guid? TaxGroupId { get; set; }
    
    [ForeignKey(nameof(TaxGroupId))]
    public virtual TaxGroup? TaxGroup { get; set; }

    // ── Matching ────────────────────────────────────────────────────────

    public InvoiceMatchingType MatchingType { get; set; } = InvoiceMatchingType.None;

    public InvoiceMatchingStatus MatchingStatus { get; set; } = InvoiceMatchingStatus.Unmatched;

    [MaxLength(2000)]
    public string? MatchingNotes { get; set; }

    // ── Status & Approval ───────────────────────────────────────────────

    public VendorInvoiceStatus Status { get; set; } = VendorInvoiceStatus.Draft;

    [MaxLength(50)]
    public string ApprovalStatus { get; set; } = "Draft"; // Draft, PendingApproval, Approved, Rejected

    public Guid? SubmittedById { get; set; }
    public DateTime? SubmittedDate { get; set; }

    public Guid? ApprovedById { get; set; }
    public DateTime? ApprovedDate { get; set; }

    [MaxLength(1000)]
    public string? ApprovalComments { get; set; }

    // ── GL Posting ──────────────────────────────────────────────────────

    public Guid? ExpenseAccountId { get; set; }
    public virtual Account? ExpenseAccount { get; set; }

    public Guid? ApAccountId { get; set; }
    public virtual Account? ApAccount { get; set; }

    public Guid? JournalEntryId { get; set; }

    // ── Notes & Reference ───────────────────────────────────────────────

    [MaxLength(500)]
    public string? Notes { get; set; }

    [MaxLength(100)]
    public string? Reference { get; set; }

    // ── Multi-tenant ────────────────────────────────────────────────────

    public Guid TenantId { get; set; }
    public virtual Tenant Tenant { get; set; } = null!;

    // ── Navigation ──────────────────────────────────────────────────────

    public virtual ICollection<VendorInvoiceLineItem> LineItems { get; set; } = new List<VendorInvoiceLineItem>();
    public virtual ICollection<VendorPaymentAllocation> PaymentAllocations { get; set; } = new List<VendorPaymentAllocation>();
}

/// <summary>
/// An individual line item on a vendor invoice.
/// </summary>
public class VendorInvoiceLineItem : TenantEntity
{
    [Required]
    public Guid VendorInvoiceId { get; set; }
    public virtual VendorInvoice VendorInvoice { get; set; } = null!;

    // ── Line Item Type ──────────────────────────────────────────────────

    /// <summary>
    /// Whether this line item maps to an inventory product or a GL expense account.
    /// </summary>
    [MaxLength(20)]
    public string LineItemType { get; set; } = "Expense"; // Expense, Product

    // ── For GL-account-based lines ──────────────────────────────────────

    public Guid? GLAccountId { get; set; }
    public virtual Account? GLAccount { get; set; }

    // ── For product-based lines (links to PO item for matching) ─────────

    public Guid? PurchaseOrderItemId { get; set; }
    public virtual PurchaseOrderItem? PurchaseOrderItem { get; set; }

    public Guid? InventoryItemId { get; set; }
    public virtual InventoryItem? InventoryItem { get; set; }

    // ── Inventory destination tracking ──────────────────────────────────
    public Guid? WarehouseId { get; set; }
    public virtual Warehouse? Warehouse { get; set; }
    
    public Guid? LocationId { get; set; }
    public virtual WarehouseLocation? Location { get; set; }

    // ── Inventory item tracking details ─────────────────────────────────
    [MaxLength(100)]
    public string? SerialNumber { get; set; }
    
    [MaxLength(100)]
    public string? LotNumber { get; set; }
    
    public DateTime? ExpirationDate { get; set; }

    // ── Description & Amounts ───────────────────────────────────────────

    [Required]
    [MaxLength(500)]
    public string Description { get; set; } = string.Empty;

    [Column(TypeName = "decimal(18,4)")]
    public decimal Quantity { get; set; } = 1;

    [Required]
    [Column(TypeName = "decimal(18,4)")]
    public decimal UnitPrice { get; set; }

    [NotMapped]
    public decimal LineTotal => Quantity * UnitPrice;

    // ── Tax ──────────────────────────────────────────────────────────────

    public Guid? TaxGroupId { get; set; }
    
    [ForeignKey(nameof(TaxGroupId))]
    public virtual TaxGroup? TaxGroup { get; set; }

    [Column(TypeName = "decimal(5,2)")]
    public decimal TaxRate { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal TaxAmount { get; set; }

    [MaxLength(50)]
    public string? TaxCode { get; set; }

    // ── Discount ────────────────────────────────────────────────────────

    [Column(TypeName = "decimal(5,2)")]
    public decimal DiscountPercentage { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal DiscountAmount { get; set; }

    // ── Unit of Measure ─────────────────────────────────────────────────

    [MaxLength(50)]
    public string? Unit { get; set; }

    // ── Multi-tenant ────────────────────────────────────────────────────

    public Guid TenantId { get; set; }
    public virtual Tenant Tenant { get; set; } = null!;
}

#endregion

#region Vendor Payment

/// <summary>
/// Represents a payment made to a supplier/vendor.
/// A single payment can be allocated across multiple invoices.
/// </summary>
public class VendorPayment : TenantEntity
{
    [Required]
    [MaxLength(50)]
    public string PaymentNumber { get; set; } = string.Empty;

    // ── Supplier ────────────────────────────────────────────────────────

    [Required]
    public Guid SupplierId { get; set; }
    public virtual Supplier Supplier { get; set; } = null!;

    // ── Financial ───────────────────────────────────────────────────────

    [Required]
    public DateTime PaymentDate { get; set; } = DateTime.UtcNow;

    [Required]
    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalAmount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal AllocatedAmount { get; set; }

    [NotMapped]
    public decimal UnallocatedAmount => TotalAmount - AllocatedAmount;

    // ── Payment Method ──────────────────────────────────────────────────

    public VendorPaymentMethod PaymentMethod { get; set; } = VendorPaymentMethod.BankTransfer;

    // ── Currency ────────────────────────────────────────────────────────

    [MaxLength(3)]
    public string CurrencyCode { get; set; } = "USD";

    [Column(TypeName = "decimal(18,6)")]
    public decimal ExchangeRate { get; set; } = 1.0m;

    // ── Bank Details ────────────────────────────────────────────────────

    public Guid? BankAccountId { get; set; }
    public virtual BankAccount? BankAccount { get; set; }

    [MaxLength(100)]
    public string? ChequeNumber { get; set; }

    [MaxLength(100)]
    public string? TransactionReference { get; set; }

    // ── Withholding Tax ─────────────────────────────────────────────────

    [Column(TypeName = "decimal(5,2)")]
    public decimal WithholdingTaxRate { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal WithholdingTaxAmount { get; set; }

    // ── Early-Payment Discount Applied ──────────────────────────────────

    [Column(TypeName = "decimal(18,2)")]
    public decimal DiscountTaken { get; set; }

    // ── Status & Authorization ──────────────────────────────────────────

    public VendorPaymentStatus Status { get; set; } = VendorPaymentStatus.Draft;

    public Guid? AuthorizedById { get; set; }
    public DateTime? AuthorizedDate { get; set; }

    public DateTime? ClearedDate { get; set; }

    // ── Batch Link ──────────────────────────────────────────────────────

    public Guid? PaymentBatchId { get; set; }
    public virtual PaymentBatch? PaymentBatch { get; set; }

    // ── GL Posting ──────────────────────────────────────────────────────

    public Guid? JournalEntryId { get; set; }

    // ── Notes ───────────────────────────────────────────────────────────

    [MaxLength(500)]
    public string? Notes { get; set; }

    // ── Multi-tenant ────────────────────────────────────────────────────

    public Guid TenantId { get; set; }
    public virtual Tenant Tenant { get; set; } = null!;

    // ── Navigation ──────────────────────────────────────────────────────

    public virtual ICollection<VendorPaymentAllocation> Allocations { get; set; } = new List<VendorPaymentAllocation>();
}

/// <summary>
/// Represents the allocation of a vendor payment to a specific vendor invoice.
/// Supports partial and multi-invoice payment allocation.
/// </summary>
public class VendorPaymentAllocation : TenantEntity
{
    [Required]
    public Guid VendorPaymentId { get; set; }
    public virtual VendorPayment VendorPayment { get; set; } = null!;

    [Required]
    public Guid VendorInvoiceId { get; set; }
    public virtual VendorInvoice VendorInvoice { get; set; } = null!;

    [Required]
    [Column(TypeName = "decimal(18,2)")]
    public decimal AllocatedAmount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal DiscountAmount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal WithholdingTaxAmount { get; set; }

    public DateTime AllocationDate { get; set; } = DateTime.UtcNow;

    [MaxLength(500)]
    public string? Notes { get; set; }

    // Track reversals
    public bool IsReversal { get; set; } = false;
    public Guid? OriginalAllocationId { get; set; }

    // ── Multi-tenant ────────────────────────────────────────────────────

    public Guid TenantId { get; set; }
    public virtual Tenant Tenant { get; set; } = null!;
}

#endregion

#region Payment Batch

/// <summary>
/// Groups vendor payments for bulk processing and authorization.
/// Payments within a batch share the same authorization workflow and processing date.
/// </summary>
public class PaymentBatch : TenantEntity
{
    [Required]
    [MaxLength(50)]
    public string BatchNumber { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? Description { get; set; }

    // ── Date Range (invoices due in this window) ────────────────────────

    public DateTime BatchDate { get; set; } = DateTime.UtcNow;

    public DateTime? DueDateFrom { get; set; }
    public DateTime? DueDateTo { get; set; }

    // ── Totals ──────────────────────────────────────────────────────────

    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalAmount { get; set; }

    public int PaymentCount { get; set; }

    // ── Payment Method ──────────────────────────────────────────────────

    public VendorPaymentMethod PaymentMethod { get; set; } = VendorPaymentMethod.BankTransfer;

    public Guid? BankAccountId { get; set; }
    public virtual BankAccount? BankAccount { get; set; }

    // ── Status & Authorization ──────────────────────────────────────────

    public PaymentBatchStatus Status { get; set; } = PaymentBatchStatus.Draft;

    public Guid? CreatedById { get; set; }
    public Guid? ApprovedById { get; set; }
    public DateTime? ApprovedDate { get; set; }

    public Guid? ProcessedById { get; set; }
    public DateTime? ProcessedDate { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }

    // ── Multi-tenant ────────────────────────────────────────────────────

    public Guid TenantId { get; set; }
    public virtual Tenant Tenant { get; set; } = null!;

    // ── Navigation ──────────────────────────────────────────────────────

    public virtual ICollection<PaymentBatchItem> Items { get; set; } = new List<PaymentBatchItem>();
}

/// <summary>
/// Links a VendorPayment to a PaymentBatch.
/// </summary>
public class PaymentBatchItem : TenantEntity
{
    [Required]
    public Guid PaymentBatchId { get; set; }
    public virtual PaymentBatch PaymentBatch { get; set; } = null!;

    [Required]
    public Guid VendorPaymentId { get; set; }
    public virtual VendorPayment VendorPayment { get; set; } = null!;

    [Column(TypeName = "decimal(18,2)")]
    public decimal Amount { get; set; }

    [MaxLength(50)]
    public string ItemStatus { get; set; } = "Pending"; // Pending, Processed, Failed

    [MaxLength(500)]
    public string? FailureReason { get; set; }

    // ── Multi-tenant ────────────────────────────────────────────────────

    public Guid TenantId { get; set; }
    public virtual Tenant Tenant { get; set; } = null!;
}

#endregion

#region Supplier Returns & Debit Notes

public enum SupplierReturnStatus
{
    Draft = 1,
    Approved = 2,
    Cancelled = 3
}

public class SupplierReturn : TenantEntity
{
    [Required]
    [MaxLength(50)]
    public string ReturnNumber { get; set; } = string.Empty;

    [Required]
    public Guid VendorId { get; set; }
    public virtual BusinessPartner Vendor { get; set; } = null!;

    [Required]
    [MaxLength(200)]
    public string VendorName { get; set; } = string.Empty;

    public Guid? OriginalVendorInvoiceId { get; set; }
    public virtual VendorInvoice? OriginalVendorInvoice { get; set; }

    public Guid? OriginalFinancePurchaseOrderReceiptId { get; set; }
    public virtual FinancePurchaseOrderReceipt? OriginalFinancePurchaseOrderReceipt { get; set; }

    [Required]
    public DateTime ReturnDate { get; set; } = DateTime.UtcNow;

    [MaxLength(500)]
    public string? Reason { get; set; }

    [Required]
    [MaxLength(3)]
    public string CurrencyCode { get; set; } = "GHS";

    [Column(TypeName = "decimal(18,6)")]
    public decimal ExchangeRate { get; set; } = 1.0m;

    [Column(TypeName = "decimal(18,2)")]
    public decimal SubTotal { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal TaxAmount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalAmount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal DiscountAmount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal BaseCurrencyAmount { get; set; }

    public SupplierReturnStatus Status { get; set; } = SupplierReturnStatus.Draft;

    public virtual ICollection<SupplierReturnLineItem> LineItems { get; set; } = new List<SupplierReturnLineItem>();
}

public class SupplierReturnLineItem : TenantEntity
{
    [Required]
    public Guid SupplierReturnId { get; set; }
    public virtual SupplierReturn SupplierReturn { get; set; } = null!;

    public Guid? OriginalVendorInvoiceLineItemId { get; set; }
    public Guid? OriginalFinancePurchaseOrderItemId { get; set; }

    [Required]
    [MaxLength(500)]
    public string Description { get; set; } = string.Empty;

    [Column(TypeName = "decimal(18,4)")]
    public decimal QuantityReturned { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal UnitPrice { get; set; }

    public Guid? TaxGroupId { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal TaxRate { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal TaxAmount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal DiscountPercentage { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal DiscountAmount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal LineTotal { get; set; }
}

public enum SupplierDebitNoteStatus
{
    Draft = 1,
    Approved = 2,
    Voided = 3
}

public class SupplierDebitNote : TenantEntity
{
    [Required]
    [MaxLength(50)]
    public string DebitNoteNumber { get; set; } = string.Empty;

    [Required]
    public Guid VendorId { get; set; }
    public virtual BusinessPartner Vendor { get; set; } = null!;

    public Guid? SupplierReturnId { get; set; }
    public virtual SupplierReturn? SupplierReturn { get; set; }

    public Guid? OriginalVendorInvoiceId { get; set; }
    public virtual VendorInvoice? OriginalVendorInvoice { get; set; }

    [Required]
    public DateTime DebitNoteDate { get; set; } = DateTime.UtcNow;

    [Required]
    [MaxLength(3)]
    public string CurrencyCode { get; set; } = "GHS";

    [Column(TypeName = "decimal(18,6)")]
    public decimal ExchangeRate { get; set; } = 1.0m;

    [Column(TypeName = "decimal(18,2)")]
    public decimal SubTotal { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal TaxAmount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalAmount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal DiscountAmount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal BaseCurrencyAmount { get; set; }

    public SupplierDebitNoteStatus Status { get; set; } = SupplierDebitNoteStatus.Draft;

    public Guid? JournalEntryId { get; set; }

    public virtual ICollection<SupplierDebitNoteLineItem> LineItems { get; set; } = new List<SupplierDebitNoteLineItem>();
}

public class SupplierDebitNoteLineItem : TenantEntity
{
    [Required]
    public Guid SupplierDebitNoteId { get; set; }
    public virtual SupplierDebitNote SupplierDebitNote { get; set; } = null!;

    [Required]
    [MaxLength(500)]
    public string Description { get; set; } = string.Empty;

    [Column(TypeName = "decimal(18,4)")]
    public decimal Quantity { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal UnitPrice { get; set; }

    public Guid? TaxGroupId { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal TaxRate { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal TaxAmount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal DiscountPercentage { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal DiscountAmount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal LineTotal { get; set; }
}

#endregion
