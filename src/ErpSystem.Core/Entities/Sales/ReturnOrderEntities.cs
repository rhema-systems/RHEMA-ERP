using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Entities.Base;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Entities.Sales;

/// <summary>
/// Return Order — customer request to return goods from a delivered sales order
/// Lifecycle: Requested → Approved → Received → Inspected → CreditIssued  (or Rejected/Cancelled)
/// </summary>
public class ReturnOrder : DocumentEntity
{
    // Reference to the original sales order
    public Guid SalesOrderId { get; set; }
    public virtual SalesOrder SalesOrder { get; set; } = null!;

    public Guid? DeliveryNoteId { get; set; }
    public virtual DeliveryNote? DeliveryNote { get; set; }

    public Guid CustomerId { get; set; }
    public virtual Customer Customer { get; set; } = null!;

    public ReturnOrderStatus ReturnStatus { get; set; } = ReturnOrderStatus.Requested;
    public ReturnReasonCode ReasonCode { get; set; }

    [StringLength(2000)]
    public string? ReasonDescription { get; set; }

    // Inspection
    public DateTime? ReceivedDate { get; set; }
    public DateTime? InspectedDate { get; set; }
    public Guid? InspectedById { get; set; }
    public virtual ApplicationUser? InspectedBy { get; set; }

    [StringLength(2000)]
    public string? InspectionNotes { get; set; }

    // Result
    public Guid? CreditNoteId { get; set; }
    public virtual CreditNote? CreditNote { get; set; }

    public Guid? RefundId { get; set; }
    public virtual Refund? Refund { get; set; }

    // Multi-tenant
    public Guid TenantId { get; set; }
    public virtual Tenant Tenant { get; set; } = null!;

    public virtual ICollection<ReturnOrderLine> Lines { get; set; } = new List<ReturnOrderLine>();
}

/// <summary>
/// Individual items being returned
/// </summary>
public class ReturnOrderLine : BaseEntity
{
    public Guid ReturnOrderId { get; set; }
    public virtual ReturnOrder ReturnOrder { get; set; } = null!;

    // Reference to the original sales order line
    public Guid? SalesOrderLineId { get; set; }
    public virtual SalesOrderLine? SalesOrderLine { get; set; }

    [Required]
    [StringLength(200)]
    public string Description { get; set; } = string.Empty;

    [StringLength(50)]
    public string? ProductCode { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal QuantityReturned { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal UnitPrice { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal LineTotal => QuantityReturned * UnitPrice;

    public ReturnReasonCode ReasonCode { get; set; }

    [StringLength(500)]
    public string? Condition { get; set; } // New, Used, Damaged, Defective

    public bool IsRestockable { get; set; } = true;

    // Multi-tenant
    public Guid TenantId { get; set; }
    public virtual Tenant Tenant { get; set; } = null!;
}

/// <summary>
/// Credit Note — issued against a return order or as a standalone adjustment
/// Lifecycle: Draft → PendingApproval → Approved → Applied (or Voided)
/// </summary>
public class CreditNote : DocumentEntity
{
    public Guid CustomerId { get; set; }
    public virtual Customer Customer { get; set; } = null!;

    public Guid? ReturnOrderId { get; set; }
    public virtual ReturnOrder? ReturnOrder { get; set; }

    public Guid? OriginalInvoiceId { get; set; }
    public virtual Invoice? OriginalInvoice { get; set; }

    public CreditNoteStatus CreditNoteStatus { get; set; } = CreditNoteStatus.Draft;

    [StringLength(2000)]
    public string? Reason { get; set; }

    // Application
    public DateTime? AppliedDate { get; set; }
    public Guid? AppliedToInvoiceId { get; set; }

    // GL Posting
    public Guid? JournalEntryId { get; set; }

    // Multi-tenant
    public Guid TenantId { get; set; }
    public virtual Tenant Tenant { get; set; } = null!;

    public virtual ICollection<CreditNoteLine> Lines { get; set; } = new List<CreditNoteLine>();
}

/// <summary>
/// Individual items on a credit note
/// </summary>
public class CreditNoteLine : BaseEntity
{
    public Guid CreditNoteId { get; set; }
    public virtual CreditNote CreditNote { get; set; } = null!;

    [Required]
    [StringLength(200)]
    public string Description { get; set; } = string.Empty;

    [Column(TypeName = "decimal(18,4)")]
    public decimal Quantity { get; set; } = 1;

    [Column(TypeName = "decimal(18,2)")]
    public decimal UnitPrice { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal LineTotal => Quantity * UnitPrice;

    [Column(TypeName = "decimal(18,2)")]
    public decimal TaxAmount { get; set; }

    [StringLength(50)]
    public string? TaxCode { get; set; }

    // Multi-tenant
    public Guid TenantId { get; set; }
    public virtual Tenant Tenant { get; set; } = null!;
}

/// <summary>
/// Refund — monetary refund to a customer (tied to a credit note or return order)
/// Lifecycle: Draft → PendingApproval → Approved → Processing → Completed (or Rejected/Cancelled)
/// </summary>
public class Refund : DocumentEntity
{
    public Guid CustomerId { get; set; }
    public virtual Customer Customer { get; set; } = null!;

    public Guid? CreditNoteId { get; set; }
    public virtual CreditNote? CreditNote { get; set; }

    public Guid? ReturnOrderId { get; set; }
    public virtual ReturnOrder? ReturnOrder { get; set; }

    public RefundStatus RefundStatus { get; set; } = RefundStatus.Draft;

    [StringLength(50)]
    public string RefundMethod { get; set; } = "BankTransfer"; // BankTransfer, CreditBalance, Cash, Other

    [Column(TypeName = "decimal(18,2)")]
    public decimal RefundAmount { get; set; }

    [StringLength(2000)]
    public string? Reason { get; set; }

    // Processing
    public DateTime? ProcessedDate { get; set; }
    public Guid? ProcessedById { get; set; }
    public virtual ApplicationUser? ProcessedBy { get; set; }

    [StringLength(100)]
    public string? PaymentReference { get; set; }

    // Multi-tenant
    public Guid TenantId { get; set; }
    public virtual Tenant Tenant { get; set; } = null!;
}
