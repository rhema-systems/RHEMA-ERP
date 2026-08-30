using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Entities.Inventory;

namespace ErpSystem.Core.Entities.Procurement;

/// <summary>
/// Request for Quotation (RFQ)
/// Private procurement request sent to selected suppliers (known / pre-qualified),
/// intended to collect price quotations with a lighter-weight process than formal tenders.
/// </summary>
public class RequestForQuotation : TenantEntity
{
    [Required]
    [MaxLength(50)]
    public string RfqNumber { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    [MaxLength(30)]
    public string Status { get; set; } = "Draft"; // Draft, Sent, Closed, Awarded, Cancelled

    public DateTime? SubmissionDeadline { get; set; }

    [MaxLength(10)]
    public string Currency { get; set; } = "USD";

    [Column(TypeName = "decimal(18,2)")]
    public decimal EstimatedValue { get; set; } = 0m;

    public Guid? SourcePurchaseRequisitionId { get; set; }
    public Guid? SourcingReleaseId { get; set; }
    public Guid? SourcingCaseId { get; set; }

    /// <summary>
    /// Optional email recipients who are not system business partners (semicolon/comma/newline separated).
    /// These recipients get email-only (no portal access).
    /// </summary>
    public string? ExternalRecipientEmails { get; set; }

    public DateTime? SentAt { get; set; }

    // Metadata
    public new Guid? CreatedById { get; set; }

    // Award info (simple RFQ flow)
    public Guid? AwardedBusinessPartnerId { get; set; }
    public DateTime? AwardedAt { get; set; }

    // Navigation
    public virtual PurchaseRequisition? SourcePurchaseRequisition { get; set; }
    public virtual ProcurementRequisitionSourcingRelease? SourcingRelease { get; set; }
    public virtual ProcurementSourcingCase? SourcingCase { get; set; }
    public virtual BusinessPartner? AwardedBusinessPartner { get; set; }
    public virtual ICollection<RequestForQuotationItem> Items { get; set; } = new List<RequestForQuotationItem>();
    public virtual ICollection<RequestForQuotationInvitation> Invitations { get; set; } = new List<RequestForQuotationInvitation>();
    public virtual ICollection<RequestForQuotationQuote> Quotes { get; set; } = new List<RequestForQuotationQuote>();

    // Awarding (internal)
    public virtual ICollection<RequestForQuotationAwardLine> AwardLines { get; set; } = new List<RequestForQuotationAwardLine>();
    public virtual ICollection<ProcurementRfqReceipt> Receipts { get; set; } = new List<ProcurementRfqReceipt>();
    public virtual ProcurementRfqOpeningRegister? OpeningRegister { get; set; }
    public virtual ProcurementRfqEvaluation? Evaluation { get; set; }
}

public class RequestForQuotationItem : TenantEntity
{
    [Required]
    public Guid RfqId { get; set; }

    /// <summary>
    /// If the RFQ was generated from a Purchase Requisition, this points to the source requisition line.
    /// Used for traceability and to update PR items when an RFQ is awarded.
    /// </summary>
    public Guid? SourcePurchaseRequisitionItemId { get; set; }

    public int LineNumber { get; set; }

    public Guid? InventoryItemId { get; set; }

    [MaxLength(50)]
    public string? ItemCode { get; set; }

    [Required]
    public string Description { get; set; } = string.Empty;

    [Column(TypeName = "decimal(18,4)")]
    public decimal Quantity { get; set; }

    [MaxLength(50)]
    public string UnitOfMeasure { get; set; } = string.Empty;

    public string? Specifications { get; set; }

    public DateTime? RequiredDeliveryDate { get; set; }

    // Navigation
    public virtual RequestForQuotation Rfq { get; set; } = null!;
    public virtual InventoryItem? InventoryItem { get; set; }
}

public class RequestForQuotationInvitation : TenantEntity
{
    [Required]
    public Guid RfqId { get; set; }

    [Required]
    public Guid BusinessPartnerId { get; set; }

    [MaxLength(30)]
    public string Status { get; set; } = "Invited"; // Selected, Invited, Opened, Responded, Revised, LateRejected, Declined

    public DateTime InvitedAt { get; set; } = DateTime.UtcNow;
    public DateTime? OpenedAt { get; set; }
    public DateTime? RespondedAt { get; set; }

    // Navigation
    public virtual RequestForQuotation Rfq { get; set; } = null!;
    public virtual BusinessPartner BusinessPartner { get; set; } = null!;
}

public class RequestForQuotationQuote : TenantEntity
{
    [Required]
    public Guid RfqId { get; set; }

    [Required]
    public Guid BusinessPartnerId { get; set; }

    public Guid? SubmittedByUserId { get; set; }

    [MaxLength(20)]
    public string Status { get; set; } = "Draft"; // Draft, Submitted

    public DateTime? SubmittedAt { get; set; }

    public string? Notes { get; set; }

    // Navigation
    public virtual RequestForQuotation Rfq { get; set; } = null!;
    public virtual BusinessPartner BusinessPartner { get; set; } = null!;
    public virtual ICollection<RequestForQuotationQuoteItem> Items { get; set; } = new List<RequestForQuotationQuoteItem>();
}

public class RequestForQuotationQuoteItem : TenantEntity
{
    [Required]
    public Guid QuoteId { get; set; }

    [Required]
    public Guid RfqItemId { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal UnitPrice { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal LineTotal { get; set; }

    // Navigation
    public virtual RequestForQuotationQuote Quote { get; set; } = null!;
    public virtual RequestForQuotationItem RfqItem { get; set; } = null!;
}

/// <summary>
/// Awarding decisions at RFQ-line level (supports both winner-takes-all and split-award).
/// This is an internal audit record of which supplier/quote was selected for each RFQ item.
/// </summary>
public class RequestForQuotationAwardLine : TenantEntity
{
    [Required]
    public Guid RfqId { get; set; }

    [Required]
    public Guid RfqItemId { get; set; }

    [Required]
    public Guid BusinessPartnerId { get; set; }

    [Required]
    public Guid QuoteId { get; set; }

    public Guid? QuoteItemId { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal UnitPrice { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal LineTotal { get; set; }

    /// <summary>
    /// Optional justification entered during split-award decision.
    /// </summary>
    [MaxLength(500)]
    public string? AwardReason { get; set; }

    // Navigation
    public virtual RequestForQuotation Rfq { get; set; } = null!;
    public virtual RequestForQuotationItem RfqItem { get; set; } = null!;
    public virtual RequestForQuotationQuote Quote { get; set; } = null!;
    public virtual BusinessPartner BusinessPartner { get; set; } = null!;
}
