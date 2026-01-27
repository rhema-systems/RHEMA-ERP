using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ErpSystem.Core.Entities.Procurement;

/// <summary>
/// Contract entity - represents a formal agreement between organization and business partner
/// Created from a tender award
/// </summary>
public class Contract : TenantEntity
{
    [Required]
    [MaxLength(50)]
    public string ContractNumber { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string ContractTitle { get; set; } = string.Empty;

    [MaxLength(50)]
    public string ContractType { get; set; } = "Service"; // Service, Works, Consultancy, Supply

    [MaxLength(50)]
    public string Status { get; set; } = "Draft"; // Draft, PendingSignature, Active, Completed, Terminated, Suspended

    // Linked Records
    [Required]
    public Guid TenderAwardId { get; set; }

    [Required]
    public Guid TenderId { get; set; }

    [Required]
    public Guid BusinessPartnerId { get; set; }

    public Guid? TenderBidId { get; set; }

    // Financial Details
    [Column(TypeName = "decimal(18,2)")]
    public decimal ContractValue { get; set; }

    [MaxLength(3)]
    public string Currency { get; set; } = "USD";

    [MaxLength(200)]
    public string? PaymentTerms { get; set; } // e.g., "30 days after invoice", "Milestone-based"

    [Column(TypeName = "decimal(5,2)")]
    public decimal RetentionPercentage { get; set; } = 0; // % held until completion (common: 5-10%)

    // Timeline
    public DateTime? StartDate { get; set; }

    public DateTime? EndDate { get; set; }

    public int? DurationDays { get; set; } // Auto-calculated or manual

    public int? WarrantyPeriodDays { get; set; } // Post-completion warranty duration

    // Scope & Terms
    public string? ScopeOfWork { get; set; }

    public string? Deliverables { get; set; }

    public string? SpecialConditions { get; set; }

    public string? PenaltyClause { get; set; } // Late delivery penalties

    // Signing Information
    public DateTime? SignedDate { get; set; }

    public Guid? SignedById { get; set; }

    [MaxLength(200)]
    public string? SignedByName { get; set; }

    [MaxLength(200)]
    public string? ContractorSignatoryName { get; set; }

    public DateTime? ContractorSignedDate { get; set; }

    // Document Reference
    [MaxLength(500)]
    public string? ContractDocumentPath { get; set; } // Path to uploaded signed contract PDF

    public string? Notes { get; set; }

    // Audit
    public Guid? CreatedById { get; set; }

    public DateTime? ActivatedAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    public DateTime? TerminatedAt { get; set; }

    [MaxLength(500)]
    public string? TerminationReason { get; set; }

    // Navigation Properties
    public virtual TenderAward TenderAward { get; set; } = null!;
    public virtual Tender Tender { get; set; } = null!;
    public virtual BusinessPartner BusinessPartner { get; set; } = null!;
    public virtual TenderBid? TenderBid { get; set; }
    public virtual ApplicationUser? SignedBy { get; set; }
    public virtual ApplicationUser? CreatedBy { get; set; }

    public virtual ICollection<ContractMilestone> Milestones { get; set; } = new List<ContractMilestone>();
    public virtual ICollection<ContractAmendment> Amendments { get; set; } = new List<ContractAmendment>();
    public virtual ICollection<ContractDocument> Documents { get; set; } = new List<ContractDocument>();
}

/// <summary>
/// Contract milestone for payment tracking
/// </summary>
public class ContractMilestone : TenantEntity
{
    [Required]
    public Guid ContractId { get; set; }

    [Required]
    [MaxLength(100)]
    public string MilestoneName { get; set; } = string.Empty;

    public string? Description { get; set; }

    public int SequenceNumber { get; set; } = 1;

    // Payment Details
    [Column(TypeName = "decimal(5,2)")]
    public decimal PaymentPercentage { get; set; } // e.g., 30% upfront

    [Column(TypeName = "decimal(18,2)")]
    public decimal PaymentAmount { get; set; } // Calculated from contract value

    // Timeline
    public DateTime? PlannedDate { get; set; }

    public DateTime? ActualDate { get; set; }

    // Status
    [MaxLength(50)]
    public string Status { get; set; } = "Pending"; // Pending, InProgress, Completed, Invoiced, Paid

    public DateTime? CompletedAt { get; set; }

    public DateTime? InvoicedAt { get; set; }

    public DateTime? PaidAt { get; set; }

    [MaxLength(50)]
    public string? InvoiceNumber { get; set; }

    public string? Notes { get; set; }

    // Navigation
    public virtual Contract Contract { get; set; } = null!;
}

/// <summary>
/// Contract amendment for tracking changes to contract
/// </summary>
public class ContractAmendment : TenantEntity
{
    [Required]
    public Guid ContractId { get; set; }

    [Required]
    [MaxLength(50)]
    public string AmendmentNumber { get; set; } = string.Empty; // e.g., AMD-001

    public int SequenceNumber { get; set; } = 1;

    [MaxLength(100)]
    public string AmendmentType { get; set; } = "ValueChange"; // ValueChange, ScopeChange, TimelineExtension, Other

    [MaxLength(500)]
    public string? Reason { get; set; }

    public string? Description { get; set; }

    // Value Change
    [Column(TypeName = "decimal(18,2)")]
    public decimal? PreviousValue { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? NewValue { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? ValueChange { get; set; } // Can be positive or negative

    // Timeline Change
    public DateTime? PreviousEndDate { get; set; }

    public DateTime? NewEndDate { get; set; }

    public int? DaysExtended { get; set; }

    // Scope Change
    public string? ScopeChanges { get; set; }

    // Approval
    [MaxLength(50)]
    public string Status { get; set; } = "Draft"; // Draft, PendingApproval, Approved, Rejected

    public DateTime? RequestedDate { get; set; }

    public Guid? RequestedById { get; set; }

    public DateTime? ApprovedDate { get; set; }

    public Guid? ApprovedById { get; set; }

    [MaxLength(500)]
    public string? ApprovalNotes { get; set; }

    // Document
    [MaxLength(500)]
    public string? DocumentPath { get; set; }

    public string? Notes { get; set; }

    // Navigation
    public virtual Contract Contract { get; set; } = null!;
    public virtual ApplicationUser? RequestedBy { get; set; }
    public virtual ApplicationUser? ApprovedBy { get; set; }
}

/// <summary>
/// Contract document attachments
/// </summary>
public class ContractDocument : TenantEntity
{
    [Required]
    public Guid ContractId { get; set; }

    [MaxLength(100)]
    public string DocumentType { get; set; } = "Contract"; // Contract, Amendment, SignedCopy, SupportingDocument

    [MaxLength(200)]
    public string FileName { get; set; } = string.Empty;

    [MaxLength(500)]
    public string FilePath { get; set; } = string.Empty;

    [MaxLength(50)]
    public string? ContentType { get; set; }

    public long? FileSize { get; set; }

    public string? Description { get; set; }

    public Guid? UploadedById { get; set; }

    // Navigation
    public virtual Contract Contract { get; set; } = null!;
    public virtual ApplicationUser? UploadedBy { get; set; }
}

