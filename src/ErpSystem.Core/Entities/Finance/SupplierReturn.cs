using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Entities.Procurement;

namespace ErpSystem.Core.Entities.Finance;

public enum SupplierReturnStatus
{
    Draft = 1,
    Approved = 2,
    Cancelled = 3,
    PendingApproval = 4,
    Rejected = 5
}

public enum SupplierDebitNoteStatus
{
    Draft = 1,
    Posted = 2,
    Cancelled = 3,
    PendingApproval = 4,
    Approved = 5,
    Rejected = 6,
    Reversed = 7
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

    public DateTime ReturnDate { get; set; } = DateTime.UtcNow;

    [MaxLength(500)]
    public string? Reason { get; set; }

    [Required]
    [MaxLength(3)]
    public string CurrencyCode { get; set; } = "GHS";

    [Column(TypeName = "decimal(18,4)")]
    public decimal ExchangeRate { get; set; } = 1m;

    [Column(TypeName = "decimal(18,2)")]
    public decimal SubTotal { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal TaxAmount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal DiscountAmount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalAmount { get; set; }

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

    [Column(TypeName = "decimal(18,2)")]
    public decimal UnitPrice { get; set; }

    public Guid? TaxGroupId { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal TaxRate { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal TaxAmount { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal DiscountPercentage { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal DiscountAmount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal LineTotal { get; set; }
}

public class SupplierDebitNote : TenantEntity
{
    [Required]
    [MaxLength(50)]
    public string DebitNoteNumber { get; set; } = string.Empty;

    /// <summary>
    /// The reference printed on the supplier's document. In the supplier's books this is a
    /// credit note; AP deliberately calls the buyer-side document a supplier debit note because
    /// posting it debits AP control and reduces the amount that the buyer owes.
    /// </summary>
    [MaxLength(100)]
    public string? SupplierCreditNoteReference { get; set; }

    [Required]
    public Guid VendorId { get; set; }
    public virtual BusinessPartner Vendor { get; set; } = null!;

    /// <summary>
    /// Canonical AP Supplier identity paired to VendorId by Finance. Nullable only for controlled
    /// migration of pre-bridge drafts; every new or posted debit note must carry the pairing.
    /// </summary>
    public Guid? SupplierId { get; set; }
    public virtual Supplier? Supplier { get; set; }

    public Guid? SupplierReturnId { get; set; }
    public virtual SupplierReturn? SupplierReturn { get; set; }

    public Guid? OriginalVendorInvoiceId { get; set; }
    public virtual VendorInvoice? OriginalVendorInvoice { get; set; }

    public DateTime DebitNoteDate { get; set; } = DateTime.UtcNow;

    [MaxLength(500)]
    public string? Reason { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }

    [Required]
    [MaxLength(3)]
    public string CurrencyCode { get; set; } = "GHS";

    [Column(TypeName = "decimal(18,6)")]
    public decimal ExchangeRate { get; set; } = 1m;

    [Column(TypeName = "decimal(18,2)")]
    public decimal SubTotal { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal TaxAmount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal DiscountAmount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalAmount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal BaseCurrencyAmount { get; set; }

    public SupplierDebitNoteStatus Status { get; set; } = SupplierDebitNoteStatus.Draft;

    public Guid? JournalEntryId { get; set; }

    /// <summary>Immutable central-posting event that created the debit note's GL entry.</summary>
    public Guid? PostingEventId { get; set; }

    public Guid? SubmittedById { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public Guid? WorkflowInstanceId { get; set; }
    public Guid? ApprovedById { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public Guid? RejectedById { get; set; }
    public DateTime? RejectedAt { get; set; }

    [MaxLength(1000)]
    public string? RejectionReason { get; set; }

    /// <summary>
    /// Identifies whether approval was obtained directly on this debit note or inherited from an
    /// approved supplier return. It is evidence only and never bypasses the required state guard.
    /// </summary>
    [MaxLength(40)]
    public string ApprovalSource { get; set; } = "SupplierDebitNoteWorkflow";

    public Guid? ReversalJournalEntryId { get; set; }
    public Guid? ReversalPostingEventId { get; set; }
    public DateTime? ReversedAt { get; set; }
    public Guid? ReversedById { get; set; }

    [MaxLength(1000)]
    public string? ReversalReason { get; set; }

    [Timestamp]
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public virtual ICollection<SupplierDebitNoteLineItem> LineItems { get; set; } = new List<SupplierDebitNoteLineItem>();
    public virtual ICollection<SupplierDebitNoteApplication> Applications { get; set; } = new List<SupplierDebitNoteApplication>();
}

public class SupplierDebitNoteLineItem : TenantEntity
{
    [Required]
    public Guid SupplierDebitNoteId { get; set; }
    public virtual SupplierDebitNote SupplierDebitNote { get; set; } = null!;

    public Guid? OriginalVendorInvoiceLineItemId { get; set; }
    public Guid? OriginalFinancePurchaseOrderItemId { get; set; }

    /// <summary>
    /// Explicit credit-side account for a standalone adjustment. Return-generated notes retain
    /// their source-line identifiers and resolve the historical inventory/expense account.
    /// </summary>
    public Guid? GLAccountId { get; set; }

    /// <summary>Frozen credit account selected from source lineage or server validation.</summary>
    public Guid? ResolvedCreditAccountId { get; set; }

    /// <summary>Original posted invoice transaction reversed by this line, when linked.</summary>
    public Guid? OriginalAccountTransactionId { get; set; }

    [Required]
    [MaxLength(20)]
    public string LineItemType { get; set; } = "Expense";

    [Required]
    [MaxLength(500)]
    public string Description { get; set; } = string.Empty;

    [Column(TypeName = "decimal(18,4)")]
    public decimal Quantity { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal UnitPrice { get; set; }

    public Guid? TaxGroupId { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal TaxRate { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal TaxAmount { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal DiscountPercentage { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal DiscountAmount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal LineTotal { get; set; }

    public virtual ICollection<SupplierDebitNoteTaxComponent> TaxComponents { get; set; }
        = new List<SupplierDebitNoteTaxComponent>();
}

/// <summary>
/// Immutable server-derived tax posting evidence for one supplier debit-note line. Linked notes
/// point back to the exact invoice tax transaction; standalone notes snapshot the effective tax
/// engine result. Procurement/Inventory return evidence remains outside this Finance-owned table.
/// </summary>
public class SupplierDebitNoteTaxComponent : TenantEntity
{
    [Required]
    public Guid SupplierDebitNoteLineItemId { get; set; }
    public virtual SupplierDebitNoteLineItem SupplierDebitNoteLineItem { get; set; } = null!;

    [Required]
    public Guid TaxId { get; set; }
    public Guid? TaxGroupId { get; set; }
    public Guid? OriginalTaxCalculationId { get; set; }
    public Guid? OriginalAccountTransactionId { get; set; }

    [Required]
    public Guid ResolvedCreditAccountId { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal BaseAmount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal TaxableAmount { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal TaxRate { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal TaxAmount { get; set; }

    public ErpSystem.Core.Enums.CompoundBasis CompoundBasis { get; set; }
    public int CalculationOrder { get; set; }
}

/// <summary>
/// Finance-owned mapping between Procurement's Business Partner and Supplier masters. The bridge
/// is the only durable identity used by AP settlement; Finance never mutates either source master.
/// </summary>
public class ApSupplierIdentityLink : TenantEntity
{
    [Required]
    public Guid BusinessPartnerId { get; set; }
    public virtual BusinessPartner BusinessPartner { get; set; } = null!;

    [Required]
    public Guid SupplierId { get; set; }
    public virtual Supplier Supplier { get; set; } = null!;

    [Required]
    [MaxLength(40)]
    public string MappingSource { get; set; } = "ExactCode";

    public bool IsVerified { get; set; }
    public DateTime? VerifiedAtUtc { get; set; }
    public Guid? VerifiedById { get; set; }
}

/// <summary>
/// Applies a posted supplier debit note to one invoice within one vendor-payment settlement.
/// The application carries no new GL entry: the debit note has already reduced AP control and
/// the payment posts only its cash/discount/WHT components. This row is the immutable subledger
/// bridge that lets invoice aging and remittance advice explain the supplier credit used.
/// </summary>
public class SupplierDebitNoteApplication : TenantEntity
{
    [Required]
    public Guid SupplierDebitNoteId { get; set; }
    public virtual SupplierDebitNote SupplierDebitNote { get; set; } = null!;

    [Required]
    public Guid VendorPaymentId { get; set; }
    public virtual VendorPayment VendorPayment { get; set; } = null!;

    [Required]
    public Guid VendorInvoiceId { get; set; }
    public virtual VendorInvoice VendorInvoice { get; set; } = null!;

    /// <summary>Amount consumed in the debit-note/invoice currency.</summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal ApplicationAmount { get; set; }

    /// <summary>Frozen functional-currency value based on the posted debit-note rate.</summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal FunctionalAmount { get; set; }

    [Required]
    [MaxLength(3)]
    public string CurrencyCode { get; set; } = "GHS";

    [Column(TypeName = "decimal(18,6)")]
    public decimal ExchangeRate { get; set; } = 1m;

    public DateTime ApplicationDate { get; set; } = DateTime.UtcNow;

    [MaxLength(500)]
    public string? Notes { get; set; }

    public bool IsReversal { get; set; }
    public Guid? OriginalApplicationId { get; set; }

    /// <summary>
    /// Filled when the linked payment posts. Until then the application is an auditable
    /// reservation and must not alter VendorInvoice.PaidAmount.
    /// </summary>
    public Guid? PaymentPostingEventId { get; set; }
    public Guid? PaymentJournalEntryId { get; set; }
    public DateTime? AppliedAt { get; set; }
}
