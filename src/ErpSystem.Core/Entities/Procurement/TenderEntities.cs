using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Entities;

namespace ErpSystem.Core.Entities.Procurement;

/// <summary>
/// Tender/RFQ master entity
/// </summary>
public class Tender : TenantEntity
{
    [Required]
    [MaxLength(50)]
    public string TenderNumber { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    [Required]
    [MaxLength(50)]
    public string TenderType { get; set; } = "RFQ"; // RFQ, RFP, ITB (Invitation to Bid)

    [Required]
    [MaxLength(50)]
    public string Status { get; set; } = "Draft"; // Draft, Published, Closed, Awarded, Cancelled

    public DateTime? PublishDate { get; set; }
    public DateTime? SubmissionDeadline { get; set; }
    public DateTime? OpeningDate { get; set; }
    public DateTime? AwardDate { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? EstimatedValue { get; set; }

    public Guid? SourcePurchaseRequisitionId { get; set; }
    public Guid? SourcingReleaseId { get; set; }
    public Guid? SourcingCaseId { get; set; }

    [MaxLength(3)]
    public string? Currency { get; set; } = "USD";

    // Eligibility Criteria
    [Column(TypeName = "decimal(3,2)")]
    public decimal? MinimumPerformanceRating { get; set; }

    public bool RequiresPrequalification { get; set; } = false;
    public bool AllowPartialBids { get; set; } = false;

    // Evaluation Criteria
    [Column(TypeName = "decimal(5,2)")]
    public decimal PriceWeightage { get; set; } = 60; // Percentage

    [Column(TypeName = "decimal(5,2)")]
    public decimal QualityWeightage { get; set; } = 20;

    [Column(TypeName = "decimal(5,2)")]
    public decimal DeliveryWeightage { get; set; } = 10;

    [Column(TypeName = "decimal(5,2)")]
    public decimal ExperienceWeightage { get; set; } = 10;

    public string? EvaluationCriteriaJson { get; set; } // Additional custom criteria (legacy - use EvaluationTemplateId instead)

    /// <summary>
    /// Reference to the evaluation template used for this tender
    /// </summary>
    public Guid? EvaluationTemplateId { get; set; }

    // QCBS (Quality and Cost Based Selection) Configuration
    /// <summary>
    /// Enable QCBS evaluation methodology
    /// </summary>
    public bool UseQCBSEvaluation { get; set; } = false;

    /// <summary>
    /// Minimum technical score required to qualify for financial evaluation (default: 80)
    /// Bids below this threshold are disqualified
    /// </summary>
    [Column(TypeName = "decimal(5,2)")]
    public decimal MinimumTechnicalScore { get; set; } = 80;

    /// <summary>
    /// Technical weight percentage for QCBS combined score (default: 60%)
    /// Formula: S = (St × TechnicalWeight%) + (Sf × FinancialWeight%)
    /// </summary>
    [Column(TypeName = "decimal(5,2)")]
    public decimal TechnicalWeight { get; set; } = 60;

    /// <summary>
    /// Financial/Price weight percentage for QCBS combined score (default: 40%)
    /// TechnicalWeight + FinancialWeight should equal 100
    /// </summary>
    [Column(TypeName = "decimal(5,2)")]
    public decimal FinancialWeight { get; set; } = 40;

    public string? RequiredDocuments { get; set; } // JSON array of required document types

    // Acceptance Declaration (Optional per tender)
    public bool RequiresAcceptanceDeclaration { get; set; } = false;

    [MaxLength(500)]
    public string? AcceptanceDeclarationDocumentPath { get; set; }

    [MaxLength(200)]
    public string? AcceptanceDeclarationDocumentName { get; set; }

    // Metadata
    public new Guid? CreatedById { get; set; }
    public Guid? PublishedById { get; set; }
    public Guid? AwardedById { get; set; }

    public string? Notes { get; set; }
    public string? TermsAndConditions { get; set; }

    // Navigation Properties
    public new virtual ApplicationUser? CreatedBy { get; set; }
    public virtual ApplicationUser? PublishedBy { get; set; }
    public virtual ApplicationUser? AwardedBy { get; set; }
    public virtual EvaluationTemplate? EvaluationTemplate { get; set; }
    public virtual PurchaseRequisition? SourcePurchaseRequisition { get; set; }
    public virtual ProcurementRequisitionSourcingRelease? SourcingRelease { get; set; }
    public virtual ProcurementSourcingCase? SourcingCase { get; set; }
    public virtual ICollection<TenderLot> Lots { get; set; } = new List<TenderLot>();
    public virtual ICollection<TenderItem> Items { get; set; } = new List<TenderItem>();
    public virtual ICollection<TenderInvitation> Invitations { get; set; } = new List<TenderInvitation>();
    public virtual ICollection<TenderBid> Bids { get; set; } = new List<TenderBid>();
    public virtual ICollection<TenderDocument> Documents { get; set; } = new List<TenderDocument>();
    public virtual ICollection<TenderAward> Awards { get; set; } = new List<TenderAward>();
    public virtual ICollection<TenderFee> Fees { get; set; } = new List<TenderFee>();
    public virtual ICollection<TenderEvaluator> Evaluators { get; set; } = new List<TenderEvaluator>();
    public virtual ICollection<TenderInterview> Interviews { get; set; } = new List<TenderInterview>();
    public virtual ICollection<TenderClarification> Clarifications { get; set; } = new List<TenderClarification>();
    public virtual ICollection<TenderRevision> Revisions { get; set; } = new List<TenderRevision>();
    public virtual ICollection<TenderViewLog> ViewLogs { get; set; } = new List<TenderViewLog>();
}

/// <summary>
/// Tender LOT - Groups related items that must be bid together
/// A tender can have multiple LOTs, and vendors must bid on all items within a LOT
/// </summary>
public class TenderLot : TenantEntity
{
    [Required]
    public Guid TenderId { get; set; }

    public int LotNumber { get; set; }

    [Required]
    [MaxLength(100)]
    public string LotCode { get; set; } = string.Empty;

    [Required]
    [MaxLength(500)]
    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? EstimatedValue { get; set; }

    [MaxLength(3)]
    public string? Currency { get; set; } = "USD";

    /// <summary>
    /// LOT Status: Active, Cancelled, Awarded
    /// </summary>
    [MaxLength(50)]
    public string Status { get; set; } = "Active";

    public DateTime? RequiredDeliveryDate { get; set; }

    [MaxLength(200)]
    public string? DeliveryLocation { get; set; }

    public string? Specifications { get; set; }

    public string? Notes { get; set; }

    /// <summary>
    /// Display order within the tender
    /// </summary>
    public int DisplayOrder { get; set; } = 0;

    // Navigation Properties
    public virtual Tender Tender { get; set; } = null!;
    public virtual ICollection<TenderItem> Items { get; set; } = new List<TenderItem>();
    public virtual ICollection<TenderBidLot> BidLots { get; set; } = new List<TenderBidLot>();
    public virtual ICollection<TenderAward> Awards { get; set; } = new List<TenderAward>();
}

/// <summary>
/// Tender line items - now belongs to a LOT
/// </summary>
public class TenderItem : TenantEntity
{
    [Required]
    public Guid TenderId { get; set; }

    /// <summary>
    /// Reference to the LOT this item belongs to.
    /// If null, item is not assigned to any LOT (for backward compatibility)
    /// </summary>
    public Guid? LotId { get; set; }

    public int LineNumber { get; set; }

    [MaxLength(200)]
    public string? ItemCode { get; set; }

    [Required]
    [MaxLength(500)]
    public string Description { get; set; } = string.Empty;

    [Column(TypeName = "decimal(18,4)")]
    public decimal Quantity { get; set; }

    [MaxLength(20)]
    public string? UnitOfMeasure { get; set; }

    public string? Specifications { get; set; }

    public DateTime? RequiredDeliveryDate { get; set; }

    [MaxLength(200)]
    public string? DeliveryLocation { get; set; }

    // Navigation Properties
    public virtual Tender Tender { get; set; } = null!;
    public virtual TenderLot? Lot { get; set; }
    public virtual ICollection<TenderBidItem> BidItems { get; set; } = new List<TenderBidItem>();
}

/// <summary>
/// Tender invitations to suppliers
/// </summary>
public class TenderInvitation : TenantEntity
{
    [Required]
    public Guid TenderId { get; set; }

    [Required]
    public Guid BusinessPartnerId { get; set; }

    public DateTime InvitedDate { get; set; } = DateTime.UtcNow;

    public Guid? InvitedById { get; set; }

    [MaxLength(50)]
    public string Status { get; set; } = "Invited"; // Invited, Viewed, Submitted, Declined

    public DateTime? ViewedDate { get; set; }
    public DateTime? ResponseDate { get; set; }

    public string? DeclineReason { get; set; }

    // Navigation Properties
    public virtual Tender Tender { get; set; } = null!;
    public virtual BusinessPartner BusinessPartner { get; set; } = null!;
    public virtual ApplicationUser? InvitedBy { get; set; }
}

/// <summary>
/// Supplier bids for tenders
/// </summary>
public class TenderBid : TenantEntity
{
    [Required]
    public Guid TenderId { get; set; }

    [Required]
    public Guid BusinessPartnerId { get; set; }

    [Required]
    [MaxLength(50)]
    public string BidNumber { get; set; } = string.Empty;

    public DateTime SubmittedDate { get; set; } = DateTime.UtcNow;

    [Required]
    [MaxLength(50)]
    public string Status { get; set; } = "Submitted"; // Submitted, Opened, UnderEvaluation, Accepted, Rejected, Withdrawn

    public DateTime? OpenedDate { get; set; }
    public Guid? OpenedById { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalBidAmount { get; set; }

    [MaxLength(3)]
    public string? Currency { get; set; } = "USD";

    public int? DeliveryDays { get; set; }

    [MaxLength(500)]
    public string? PaymentTerms { get; set; }

    [MaxLength(500)]
    public string? WarrantyTerms { get; set; }

    public string? TechnicalProposal { get; set; }
    public string? CommercialProposal { get; set; }

    // Association and Acceptance (New fields for GHANEPS-style flow)
    [MaxLength(50)]
    public string? AssociationType { get; set; } // AllUsers, Self, SelectedUsers

    public bool AcceptedDeclaration { get; set; } = false;
    public DateTime? DeclarationAcceptedAt { get; set; }

    public bool IsCompliant { get; set; } = true;
    public string? NonComplianceReasons { get; set; }

    // Evaluation Scores
    [Column(TypeName = "decimal(5,2)")]
    public decimal? PriceScore { get; set; }

    [Column(TypeName = "decimal(5,2)")]
    public decimal? QualityScore { get; set; }

    [Column(TypeName = "decimal(5,2)")]
    public decimal? DeliveryScore { get; set; }

    [Column(TypeName = "decimal(5,2)")]
    public decimal? ExperienceScore { get; set; }

    [Column(TypeName = "decimal(5,2)")]
    public decimal? TotalScore { get; set; }

    public int? Rank { get; set; }

    // QCBS (Quality and Cost Based Selection) Scores
    /// <summary>
    /// Technical score from evaluators (St) - average of all evaluator scores
    /// This is the raw technical evaluation score out of 100
    /// </summary>
    [Column(TypeName = "decimal(5,2)")]
    public decimal? TechnicalScore { get; set; }

    /// <summary>
    /// Calculated financial score (Sf) using formula: Sf = 100 × (Fm / F)
    /// Where Fm = lowest bid price, F = this bid's price
    /// </summary>
    [Column(TypeName = "decimal(5,2)")]
    public decimal? FinancialScore { get; set; }

    /// <summary>
    /// Combined QCBS score: S = (St × T%) + (Sf × P%)
    /// Where T = Technical weight, P = Financial weight
    /// </summary>
    [Column(TypeName = "decimal(5,2)")]
    public decimal? CombinedScore { get; set; }

    /// <summary>
    /// Whether this bid passed the minimum technical score threshold
    /// Bids that don't pass are disqualified from financial evaluation
    /// </summary>
    public bool IsQualifiedTechnically { get; set; } = true;

    /// <summary>
    /// Reason for technical disqualification if IsQualifiedTechnically = false
    /// </summary>
    [MaxLength(500)]
    public string? DisqualificationReason { get; set; }

    // Metadata
    public Guid? EvaluatedById { get; set; }
    public DateTime? EvaluatedDate { get; set; }

    public string? EvaluationNotes { get; set; }
    public string? RejectionReason { get; set; }

    // Navigation Properties
    public virtual Tender Tender { get; set; } = null!;
    public virtual BusinessPartner BusinessPartner { get; set; } = null!;
    public virtual ApplicationUser? EvaluatedBy { get; set; }
    public virtual ApplicationUser? OpenedBy { get; set; }
    public virtual ICollection<TenderBidLot> BidLots { get; set; } = new List<TenderBidLot>();
    public virtual ICollection<TenderBidItem> Items { get; set; } = new List<TenderBidItem>();
    public virtual ICollection<TenderBidDocument> Documents { get; set; } = new List<TenderBidDocument>();
    public virtual ICollection<TenderEvaluation> Evaluations { get; set; } = new List<TenderEvaluation>();
    public virtual ICollection<TenderInterview> Interviews { get; set; } = new List<TenderInterview>();
}

/// <summary>
/// Bid for a specific LOT - represents a vendor's bid on a complete LOT
/// When bidding on a LOT, vendor must provide pricing for ALL items in that LOT
/// </summary>
public class TenderBidLot : TenantEntity
{
    [Required]
    public Guid TenderBidId { get; set; }

    [Required]
    public Guid LotId { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalLotAmount { get; set; }

    [MaxLength(3)]
    public string? Currency { get; set; } = "USD";

    public int? DeliveryDays { get; set; }

    [MaxLength(500)]
    public string? PaymentTerms { get; set; }

    [MaxLength(500)]
    public string? WarrantyTerms { get; set; }

    public string? TechnicalProposal { get; set; }
    public string? CommercialProposal { get; set; }

    /// <summary>
    /// Status of this LOT bid: Draft, Submitted, Accepted, Rejected
    /// </summary>
    [MaxLength(50)]
    public string Status { get; set; } = "Draft";

    // Evaluation Scores for this LOT
    [Column(TypeName = "decimal(5,2)")]
    public decimal? PriceScore { get; set; }

    [Column(TypeName = "decimal(5,2)")]
    public decimal? QualityScore { get; set; }

    [Column(TypeName = "decimal(5,2)")]
    public decimal? DeliveryScore { get; set; }

    [Column(TypeName = "decimal(5,2)")]
    public decimal? TotalScore { get; set; }

    public int? Rank { get; set; }

    public string? EvaluationNotes { get; set; }

    public string? Notes { get; set; }

    // Navigation Properties
    public virtual TenderBid TenderBid { get; set; } = null!;
    public virtual TenderLot Lot { get; set; } = null!;
    public virtual ICollection<TenderBidItem> Items { get; set; } = new List<TenderBidItem>();
}

/// <summary>
/// Bid line items
/// </summary>
public class TenderBidItem : TenantEntity
{
    [Required]
    public Guid TenderBidId { get; set; }

    /// <summary>
    /// Reference to the LOT bid this item belongs to.
    /// If null, item is a direct bid item (for backward compatibility)
    /// </summary>
    public Guid? BidLotId { get; set; }

    [Required]
    public Guid TenderItemId { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal OfferedQuantity { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal UnitPrice { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalPrice { get; set; }

    public int? DeliveryDays { get; set; }

    [MaxLength(500)]
    public string? Specifications { get; set; }

    [MaxLength(200)]
    public string? Brand { get; set; }

    [MaxLength(200)]
    public string? Model { get; set; }

    public string? TechnicalDetails { get; set; }

    // Navigation Properties
    public virtual TenderBid TenderBid { get; set; } = null!;
    public virtual TenderBidLot? BidLot { get; set; }
    public virtual TenderItem TenderItem { get; set; } = null!;
}

/// <summary>
/// Bid documents (technical/commercial proposals, certificates, etc.)
/// </summary>
public class TenderBidDocument : TenantEntity
{
    [Required]
    public Guid TenderBidId { get; set; }

    [Required]
    [MaxLength(200)]
    public string DocumentName { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    public string DocumentType { get; set; } = string.Empty; // TechnicalProposal, CommercialProposal, Certificate, Other

    [Required]
    [MaxLength(500)]
    public string FilePath { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? FileType { get; set; }

    public long? FileSize { get; set; }

    public DateTime UploadedDate { get; set; } = DateTime.UtcNow;

    public Guid? UploadedById { get; set; }

    // Navigation Properties
    public virtual TenderBid TenderBid { get; set; } = null!;
    public virtual ApplicationUser? UploadedBy { get; set; }
}

/// <summary>
/// Tender documents (specifications, terms, etc.)
/// </summary>
public class TenderDocument : TenantEntity
{
    [Required]
    public Guid TenderId { get; set; }

    [Required]
    [MaxLength(200)]
    public string DocumentName { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    public string DocumentType { get; set; } = string.Empty; // Specification, Terms, Drawing, Other

    [Required]
    [MaxLength(500)]
    public string FilePath { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? FileType { get; set; }

    public long? FileSize { get; set; }

    public DateTime UploadedDate { get; set; } = DateTime.UtcNow;

    public Guid? UploadedById { get; set; }

    public bool IsPublic { get; set; } = true; // Visible to all bidders

    // Navigation Properties
    public virtual Tender Tender { get; set; } = null!;
    public virtual ApplicationUser? UploadedBy { get; set; }
}

/// <summary>
/// Tender award decisions - can be at tender level or per-LOT
/// </summary>
public class TenderAward : TenantEntity
{
    [Required]
    public Guid TenderId { get; set; }

    /// <summary>
    /// Reference to the LOT being awarded.
    /// If null, this is a tender-level award (for backward compatibility or non-LOT tenders)
    /// </summary>
    public Guid? LotId { get; set; }

    /// <summary>
    /// Reference to the LOT bid being awarded.
    /// If null, this is a tender-level award
    /// </summary>
    public Guid? BidLotId { get; set; }

    [Required]
    public Guid TenderBidId { get; set; }

    [Required]
    public Guid BusinessPartnerId { get; set; }

    public DateTime AwardDate { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Original bid amount before any negotiation
    /// </summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal OriginalBidAmount { get; set; }

    /// <summary>
    /// Final award amount (uses negotiated amount if negotiation exists, otherwise uses original bid amount)
    /// </summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal AwardedAmount { get; set; }

    /// <summary>
    /// Reference to the negotiation that produced the final award amount (if any)
    /// </summary>
    public Guid? NegotiationId { get; set; }

    /// <summary>
    /// Indicates if this award was based on a negotiated price
    /// </summary>
    public bool IsNegotiated { get; set; } = false;

    [MaxLength(3)]
    public string? Currency { get; set; } = "USD";

    public Guid? AwardedById { get; set; }

    public string? AwardJustification { get; set; }

    [MaxLength(50)]
    public string Status { get; set; } = "Awarded"; // Awarded, ContractSigned, Cancelled

    public Guid? PurchaseOrderId { get; set; } // Link to generated PO

    public string? Notes { get; set; }

    // Navigation Properties
    public virtual Tender Tender { get; set; } = null!;
    public virtual TenderLot? Lot { get; set; }
    public virtual TenderBidLot? BidLot { get; set; }
    public virtual TenderBid TenderBid { get; set; } = null!;
    public virtual BusinessPartner BusinessPartner { get; set; } = null!;
    public virtual ApplicationUser? AwardedBy { get; set; }
    public virtual TenderNegotiation? Negotiation { get; set; }
}

/// <summary>
/// Performance bond request for awarded bids
/// </summary>
public class PerformanceBondRequest : TenantEntity
{
    [Required]
    public Guid TenderAwardId { get; set; }

    [Required]
    public Guid TenderBidId { get; set; }

    [Required]
    public Guid BusinessPartnerId { get; set; }

    [Required]
    [MaxLength(50)]
    public string Status { get; set; } = "Pending"; // Pending, Submitted, Approved, Rejected

    // Template uploaded by internal user
    [MaxLength(500)]
    public string? TemplateFilePath { get; set; }

    [MaxLength(200)]
    public string? TemplateFileName { get; set; }

    [MaxLength(100)]
    public string? TemplateFileType { get; set; }

    public long? TemplateFileSize { get; set; }

    public DateTime RequestedDate { get; set; } = DateTime.UtcNow;

    public Guid? RequestedById { get; set; }

    // Document submitted by external user (business partner)
    [MaxLength(500)]
    public string? SubmittedFilePath { get; set; }

    [MaxLength(200)]
    public string? SubmittedFileName { get; set; }

    [MaxLength(100)]
    public string? SubmittedFileType { get; set; }

    public long? SubmittedFileSize { get; set; }

    public DateTime? SubmittedDate { get; set; }

    public Guid? SubmittedById { get; set; }

    // Review information
    public DateTime? ReviewedDate { get; set; }

    public Guid? ReviewedById { get; set; }

    [MaxLength(1000)]
    public string? RejectionReason { get; set; }

    public string? Notes { get; set; }

    // Navigation Properties
    public virtual TenderAward TenderAward { get; set; } = null!;
    public virtual TenderBid TenderBid { get; set; } = null!;
    public virtual BusinessPartner BusinessPartner { get; set; } = null!;
    public virtual ApplicationUser? RequestedBy { get; set; }
    public virtual ApplicationUser? SubmittedBy { get; set; }
    public virtual ApplicationUser? ReviewedBy { get; set; }
}

/// <summary>
/// Tender fee structure for document purchase
/// </summary>
public class TenderFee : TenantEntity
{
    [Required]
    public Guid TenderId { get; set; }

    [Required]
    [MaxLength(100)]
    public string FeeType { get; set; } = "DocumentFee"; // DocumentFee, BidBond, PerformanceBond

    [Required]
    [Column(TypeName = "decimal(18,2)")]
    public decimal Amount { get; set; }

    [MaxLength(3)]
    public string Currency { get; set; } = "USD";

    [Required]
    [MaxLength(50)]
    public string PaymentMethod { get; set; } = "Online"; // Online, BankTransfer, Cash, Cheque

    public bool IsMandatory { get; set; } = true;

    public DateTime? DueDate { get; set; }

    public string? Description { get; set; }

    [MaxLength(200)]
    public string? BankAccountDetails { get; set; }

    // Navigation Properties
    public virtual Tender Tender { get; set; } = null!;
    public virtual ICollection<TenderPayment> Payments { get; set; } = new List<TenderPayment>();
}

/// <summary>
/// Tender payment tracking
/// </summary>
public class TenderPayment : TenantEntity
{
    [Required]
    public Guid TenderFeeId { get; set; }

    [Required]
    public Guid BusinessPartnerId { get; set; }

    [Required]
    [MaxLength(50)]
    public string PaymentReference { get; set; } = string.Empty;

    [Required]
    [Column(TypeName = "decimal(18,2)")]
    public decimal Amount { get; set; }

    [MaxLength(3)]
    public string Currency { get; set; } = "USD";

    [Required]
    [MaxLength(50)]
    public string PaymentMethod { get; set; } = "Online"; // Online, BankTransfer, Cash, Cheque

    [Required]
    [MaxLength(50)]
    public string Status { get; set; } = "Pending"; // Pending, Completed, Failed, Refunded

    public DateTime PaymentDate { get; set; } = DateTime.UtcNow;

    public DateTime? VerifiedDate { get; set; }

    public Guid? VerifiedById { get; set; }

    [MaxLength(500)]
    public string? TransactionId { get; set; }

    public string? PaymentProof { get; set; } // File path to payment receipt

    public string? Notes { get; set; }

    // Navigation Properties
    public virtual TenderFee TenderFee { get; set; } = null!;
    public virtual BusinessPartner BusinessPartner { get; set; } = null!;
    public virtual ApplicationUser? VerifiedBy { get; set; }
}

/// <summary>
/// Tender evaluator assignments for multi-evaluator workflow
/// </summary>
public class TenderEvaluator : TenantEntity
{
    [Required]
    public Guid TenderId { get; set; }

    [Required]
    public Guid UserId { get; set; }

    [Required]
    [MaxLength(50)]
    public string Role { get; set; } = "Evaluator"; // Evaluator, ChairPerson, Secretary, Observer

    public DateTime AssignedDate { get; set; } = DateTime.UtcNow;

    public Guid? AssignedById { get; set; }

    [MaxLength(50)]
    public string Status { get; set; } = "Assigned"; // Assigned, Accepted, Declined, Completed

    public DateTime? AcceptedDate { get; set; }

    public DateTime? CompletedDate { get; set; }

    [Column(TypeName = "decimal(5,2)")]
    public decimal? WeightagePercentage { get; set; } // For weighted average of evaluations

    public string? Notes { get; set; }

    // Navigation Properties
    public virtual Tender Tender { get; set; } = null!;
    public virtual ApplicationUser User { get; set; } = null!;
    public virtual ApplicationUser? AssignedBy { get; set; }
    public virtual ICollection<TenderEvaluation> Evaluations { get; set; } = new List<TenderEvaluation>();
}

/// <summary>
/// Individual evaluator scores for each bid
/// </summary>
public class TenderEvaluation : TenantEntity
{
    [Required]
    public Guid TenderBidId { get; set; }

    [Required]
    public Guid TenderEvaluatorId { get; set; }

    public DateTime EvaluationDate { get; set; } = DateTime.UtcNow;

    [Required]
    [MaxLength(50)]
    public string Status { get; set; } = "Draft"; // Draft, Submitted, Approved

    // Evaluation Scores (0-100)
    [Column(TypeName = "decimal(5,2)")]
    public decimal? PriceScore { get; set; }

    [Column(TypeName = "decimal(5,2)")]
    public decimal? QualityScore { get; set; }

    [Column(TypeName = "decimal(5,2)")]
    public decimal? DeliveryScore { get; set; }

    [Column(TypeName = "decimal(5,2)")]
    public decimal? ExperienceScore { get; set; }

    [Column(TypeName = "decimal(5,2)")]
    public decimal? TechnicalScore { get; set; }

    [Column(TypeName = "decimal(5,2)")]
    public decimal? ComplianceScore { get; set; }

    [Column(TypeName = "decimal(5,2)")]
    public decimal? TotalScore { get; set; }

    public string? EvaluationCriteriaJson { get; set; } // Additional custom criteria scores

    public string? TechnicalComments { get; set; }
    public string? CommercialComments { get; set; }
    public string? OverallComments { get; set; }

    public bool IsRecommended { get; set; } = false;

    public string? Recommendation { get; set; }

    public DateTime? SubmittedDate { get; set; }

    // Navigation Properties
    public virtual TenderBid TenderBid { get; set; } = null!;
    public virtual TenderEvaluator TenderEvaluator { get; set; } = null!;
}

/// <summary>
/// Interview and presentation management
/// </summary>
public class TenderInterview : TenantEntity
{
    [Required]
    public Guid TenderId { get; set; }

    [Required]
    public Guid TenderBidId { get; set; }

    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    public DateTime ScheduledDate { get; set; }

    public DateTime? StartTime { get; set; }

    public DateTime? EndTime { get; set; }

    [MaxLength(50)]
    public string InterviewType { get; set; } = "Virtual"; // Virtual, InPerson

    [MaxLength(500)]
    public string? Location { get; set; } // Physical location or meeting link

    [MaxLength(50)]
    public string Status { get; set; } = "Scheduled"; // Scheduled, Completed, Cancelled, Rescheduled

    public string? Agenda { get; set; }

    public string? InterviewNotes { get; set; }

    [Column(TypeName = "decimal(5,2)")]
    public decimal? InterviewScore { get; set; }

    public string? PanelMembers { get; set; } // JSON array of user IDs

    public Guid? ConductedById { get; set; }

    public DateTime? CompletedDate { get; set; }

    public string? Outcome { get; set; }

    // Navigation Properties
    public virtual Tender Tender { get; set; } = null!;
    public virtual TenderBid TenderBid { get; set; } = null!;
    public virtual ApplicationUser? ConductedBy { get; set; }
}

/// <summary>
/// Tender clarifications and Q&A
/// </summary>
public class TenderClarification : TenantEntity
{
    [Required]
    public Guid TenderId { get; set; }

    public Guid? BusinessPartnerId { get; set; } // Null if question is from internal team

    [Required]
    public string Question { get; set; } = string.Empty;

    public DateTime QuestionDate { get; set; } = DateTime.UtcNow;

    public Guid? QuestionById { get; set; }

    public string? Answer { get; set; }

    public DateTime? AnswerDate { get; set; }

    public Guid? AnsweredById { get; set; }

    [MaxLength(50)]
    public string Status { get; set; } = "Pending"; // Pending, Answered, Closed

    public bool IsPublic { get; set; } = true; // Visible to all bidders

    [MaxLength(100)]
    public string? Category { get; set; } // Technical, Commercial, Administrative

    // Navigation Properties
    public virtual Tender Tender { get; set; } = null!;
    public virtual BusinessPartner? BusinessPartner { get; set; }
    public virtual ApplicationUser? QuestionBy { get; set; }
    public virtual ApplicationUser? AnsweredBy { get; set; }
}

/// <summary>
/// Tender revisions and amendments tracking
/// </summary>
public class TenderRevision : TenantEntity
{
    [Required]
    public Guid TenderId { get; set; }

    [Required]
    [MaxLength(20)]
    public string RevisionNumber { get; set; } = string.Empty;

    public DateTime RevisionDate { get; set; } = DateTime.UtcNow;

    public Guid? RevisedById { get; set; }

    [Required]
    [MaxLength(200)]
    public string RevisionType { get; set; } = "Amendment"; // Amendment, Addendum, Corrigendum, Extension

    [Required]
    public string Description { get; set; } = string.Empty;

    public string? Changes { get; set; } // JSON of changed fields

    public DateTime? NewSubmissionDeadline { get; set; }

    public bool RequiresRebid { get; set; } = false;

    public bool NotificationSent { get; set; } = false;

    public DateTime? NotificationSentDate { get; set; }

    // Navigation Properties
    public virtual Tender Tender { get; set; } = null!;
    public virtual ApplicationUser? RevisedBy { get; set; }
}

/// <summary>
/// Tender templates for reusable tender configurations
/// </summary>
public class TenderTemplate : TenantEntity
{
    [Required]
    [MaxLength(100)]
    public string TemplateName { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    [Required]
    [MaxLength(50)]
    public string TenderType { get; set; } = "RFQ"; // RFQ, RFP, ITB

    [MaxLength(50)]
    public string Category { get; set; } = "General"; // General, Construction, IT, Services, Goods

    // Evaluation Criteria Defaults
    [Column(TypeName = "decimal(5,2)")]
    public decimal PriceWeightage { get; set; } = 60;

    [Column(TypeName = "decimal(5,2)")]
    public decimal QualityWeightage { get; set; } = 20;

    [Column(TypeName = "decimal(5,2)")]
    public decimal DeliveryWeightage { get; set; } = 10;

    [Column(TypeName = "decimal(5,2)")]
    public decimal ExperienceWeightage { get; set; } = 10;

    public string? EvaluationCriteriaJson { get; set; } // Additional custom criteria

    public string? TermsAndConditions { get; set; }

    public string? RequiredDocuments { get; set; } // JSON array of required document types

    public int? DefaultValidityDays { get; set; }

    public bool RequiresPrequalification { get; set; } = false;

    public bool AllowPartialBids { get; set; } = false;

    public bool IsActive { get; set; } = true;

    public new Guid? CreatedById { get; set; }

    // Navigation Properties
    public new virtual ApplicationUser? CreatedBy { get; set; }
}

/// <summary>
/// Tender view and download tracking for analytics
/// </summary>
public class TenderViewLog : TenantEntity
{
    [Required]
    public Guid TenderId { get; set; }

    public Guid? BusinessPartnerId { get; set; }

    public Guid? UserId { get; set; }

    [Required]
    [MaxLength(50)]
    public string ActionType { get; set; } = "View"; // View, Download, DocumentDownload

    public DateTime ActionDate { get; set; } = DateTime.UtcNow;

    [MaxLength(200)]
    public string? IpAddress { get; set; }

    [MaxLength(500)]
    public string? UserAgent { get; set; }

    public Guid? DocumentId { get; set; } // For document download tracking

    [MaxLength(500)]
    public string? DocumentName { get; set; }

    // Navigation Properties
    public virtual Tender Tender { get; set; } = null!;
    public virtual BusinessPartner? BusinessPartner { get; set; }
    public virtual ApplicationUser? User { get; set; }
}

/// <summary>
/// Evaluation criteria master data for tender evaluation
/// </summary>
public class EvaluationCriterion : TenantEntity
{
    [Required]
    [MaxLength(100)]
    public string CriterionName { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    public string CriterionCode { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    public string Category { get; set; } = "General"; // Financial, Technical, Experience, Schedule, Quality, Other

    /// <summary>
    /// Specifies whether this criterion is used for Technical or Financial evaluation in QCBS.
    /// Technical criteria contribute to the Technical Score.
    /// Financial criteria contribute to the Financial Score (alongside price-based calculation).
    /// </summary>
    [Required]
    [MaxLength(20)]
    public string EvaluationType { get; set; } = "Technical"; // Technical, Financial

    [MaxLength(500)]
    public string? Description { get; set; }

    [Range(0, 100)]
    public int MaxScore { get; set; } = 100;

    [Range(0, 100)]
    [Column(TypeName = "decimal(5,2)")]
    public decimal Weight { get; set; } = 0; // Weight percentage

    public bool IsActive { get; set; } = true;

    public int DisplayOrder { get; set; } = 0;

    public new Guid? CreatedById { get; set; }

    // Navigation Properties
    public new virtual ApplicationUser? CreatedBy { get; set; }
    public virtual ICollection<EvaluationTemplateCriterion> TemplateCriteria { get; set; } = new List<EvaluationTemplateCriterion>();
}

/// <summary>
/// Evaluation template for grouping criteria with specific weights for tender evaluation
/// </summary>
public class EvaluationTemplate : TenantEntity
{
    [Required]
    [MaxLength(100)]
    public string TemplateName { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    public string TemplateCode { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    [Required]
    [MaxLength(50)]
    public string Category { get; set; } = "General"; // General, Construction, IT, Services, Goods, Consultancy

    [Required]
    [MaxLength(50)]
    public string TenderType { get; set; } = "RFQ"; // RFQ, RFP, ITB, EOI

    /// <summary>
    /// Whether this is the default template for its category and tender type
    /// </summary>
    public bool IsDefault { get; set; } = false;

    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Minimum total score required to pass evaluation (0-100)
    /// </summary>
    [Range(0, 100)]
    [Column(TypeName = "decimal(5,2)")]
    public decimal PassingScore { get; set; } = 70;

    /// <summary>
    /// Evaluation method: SimpleAverage, WeightedAverage, PassFail, QCBS
    /// </summary>
    [Required]
    [MaxLength(50)]
    public string ScoringMethod { get; set; } = "WeightedAverage";

    public int DisplayOrder { get; set; } = 0;

    // QCBS Configuration (used when ScoringMethod = "QCBS")
    /// <summary>
    /// Weight percentage for technical score in QCBS evaluation (0-100)
    /// </summary>
    [Range(0, 100)]
    [Column(TypeName = "decimal(5,2)")]
    public decimal TechnicalWeight { get; set; } = 80;

    /// <summary>
    /// Weight percentage for financial score in QCBS evaluation (0-100)
    /// TechnicalWeight + FinancialWeight should equal 100
    /// </summary>
    [Range(0, 100)]
    [Column(TypeName = "decimal(5,2)")]
    public decimal FinancialWeight { get; set; } = 20;

    /// <summary>
    /// Minimum technical score required to qualify for financial evaluation (0-100)
    /// Bids scoring below this are disqualified
    /// </summary>
    [Range(0, 100)]
    [Column(TypeName = "decimal(5,2)")]
    public decimal MinimumTechnicalScore { get; set; } = 70;

    public new Guid? CreatedById { get; set; }

    // Navigation Properties
    public new virtual ApplicationUser? CreatedBy { get; set; }
    public virtual ICollection<EvaluationTemplateCriterion> TemplateCriteria { get; set; } = new List<EvaluationTemplateCriterion>();
    public virtual ICollection<Tender> Tenders { get; set; } = new List<Tender>();
}

/// <summary>
/// Join entity linking evaluation templates to criteria with template-specific weights
/// </summary>
public class EvaluationTemplateCriterion : TenantEntity
{
    [Required]
    public Guid EvaluationTemplateId { get; set; }

    [Required]
    public Guid EvaluationCriterionId { get; set; }

    /// <summary>
    /// Weight percentage for this criterion in this template (0-100)
    /// All weights in a template should sum to 100
    /// </summary>
    [Required]
    [Range(0, 100)]
    [Column(TypeName = "decimal(5,2)")]
    public decimal Weight { get; set; } = 0;

    /// <summary>
    /// Maximum score for this criterion in this template
    /// </summary>
    [Range(0, 100)]
    public int MaxScore { get; set; } = 100;

    /// <summary>
    /// Whether this criterion is mandatory in this template
    /// </summary>
    public bool IsMandatory { get; set; } = true;

    /// <summary>
    /// Minimum score required to pass this criterion (0-100)
    /// </summary>
    [Range(0, 100)]
    [Column(TypeName = "decimal(5,2)")]
    public decimal? MinimumScore { get; set; }

    /// <summary>
    /// Display order within the template
    /// </summary>
    public int DisplayOrder { get; set; } = 0;

    // Navigation Properties
    public virtual EvaluationTemplate EvaluationTemplate { get; set; } = null!;
    public virtual EvaluationCriterion EvaluationCriterion { get; set; } = null!;
}

/// <summary>
/// Document type master data for tender requirements
/// </summary>
public class TenderDocumentType : TenantEntity
{
    [Required]
    [MaxLength(100)]
    public string DocumentName { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    public string DocumentCode { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    public string Category { get; set; } = "General"; // Legal, Financial, Technical, Experience, General, Other

    [MaxLength(500)]
    public string? Description { get; set; }

    public bool IsRequired { get; set; } = true;

    [Range(1, 100)]
    public int MaxFileSizeMB { get; set; } = 10;

    [MaxLength(200)]
    public string AllowedFileTypes { get; set; } = "PDF,DOC,DOCX";

    public bool IsActive { get; set; } = true;

    public int DisplayOrder { get; set; } = 0;

    public new Guid? CreatedById { get; set; }

    // Navigation Properties
    public new virtual ApplicationUser? CreatedBy { get; set; }
}

/// <summary>
/// Tender negotiation - tracks price negotiations with a bidder before final award
/// </summary>
public class TenderNegotiation : TenantEntity
{
    [Required]
    public Guid TenderId { get; set; }

    [Required]
    public Guid TenderBidId { get; set; }

    [Required]
    public Guid BusinessPartnerId { get; set; }

    /// <summary>
    /// Optional reference to specific LOT being negotiated
    /// </summary>
    public Guid? LotId { get; set; }

    /// <summary>
    /// Optional reference to specific bid LOT being negotiated
    /// </summary>
    public Guid? BidLotId { get; set; }

    [MaxLength(50)]
    public string Status { get; set; } = "Invited"; // Invited, InProgress, Completed, Cancelled

    public DateTime InvitedDate { get; set; } = DateTime.UtcNow;

    public Guid? InvitedById { get; set; }

    public DateTime? CompletedDate { get; set; }

    public Guid? CompletedById { get; set; }

    /// <summary>
    /// Original total bid amount before negotiation
    /// </summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal OriginalAmount { get; set; }

    /// <summary>
    /// Final negotiated total amount
    /// </summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal? NegotiatedAmount { get; set; }

    [MaxLength(3)]
    public string? Currency { get; set; } = "USD";

    public string? Notes { get; set; }

    // Navigation Properties
    public virtual Tender Tender { get; set; } = null!;
    public virtual TenderBid TenderBid { get; set; } = null!;
    public virtual BusinessPartner BusinessPartner { get; set; } = null!;
    public virtual TenderLot? Lot { get; set; }
    public virtual TenderBidLot? BidLot { get; set; }
    public virtual ApplicationUser? InvitedBy { get; set; }
    public virtual ApplicationUser? CompletedBy { get; set; }
    public virtual ICollection<TenderNegotiationItem> Items { get; set; } = new List<TenderNegotiationItem>();
}

/// <summary>
/// Individual item in a negotiation with original and negotiated prices
/// </summary>
public class TenderNegotiationItem : TenantEntity
{
    [Required]
    public Guid NegotiationId { get; set; }

    [Required]
    public Guid TenderBidItemId { get; set; }

    /// <summary>
    /// Item description from the bid item
    /// </summary>
    [MaxLength(500)]
    public string ItemDescription { get; set; } = string.Empty;

    /// <summary>
    /// Quantity from the bid
    /// </summary>
    [Column(TypeName = "decimal(18,4)")]
    public decimal Quantity { get; set; }

    /// <summary>
    /// Unit of measure
    /// </summary>
    [MaxLength(50)]
    public string? UnitOfMeasure { get; set; }

    /// <summary>
    /// Original unit price from the bid
    /// </summary>
    [Column(TypeName = "decimal(18,4)")]
    public decimal OriginalUnitPrice { get; set; }

    /// <summary>
    /// Original total price from the bid
    /// </summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal OriginalTotalPrice { get; set; }

    /// <summary>
    /// Negotiated unit price (can be edited)
    /// </summary>
    [Column(TypeName = "decimal(18,4)")]
    public decimal? NegotiatedUnitPrice { get; set; }

    /// <summary>
    /// Negotiated total price (calculated from negotiated unit price * quantity)
    /// </summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal? NegotiatedTotalPrice { get; set; }

    public string? Notes { get; set; }

    // Navigation Properties
    public virtual TenderNegotiation Negotiation { get; set; } = null!;
    public virtual TenderBidItem TenderBidItem { get; set; } = null!;
}
