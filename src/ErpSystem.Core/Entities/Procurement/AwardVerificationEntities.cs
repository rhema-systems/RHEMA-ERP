using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ErpSystem.Core.Entities.Procurement;

/// <summary>
/// Template for award verification checklists - configured in admin
/// </summary>
public class AwardVerificationChecklistTemplate : TenantEntity
{
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    /// <summary>
    /// Category for organizing templates (e.g., "Standard", "High-Value", "Emergency")
    /// </summary>
    [MaxLength(50)]
    public string? Category { get; set; }

    /// <summary>
    /// Minimum contract value threshold for this template to be applicable
    /// </summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal? MinContractValue { get; set; }

    /// <summary>
    /// Maximum contract value threshold for this template to be applicable
    /// </summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal? MaxContractValue { get; set; }

    public bool IsActive { get; set; } = true;

    public bool IsDefault { get; set; } = false;

    public int DisplayOrder { get; set; } = 0;

    // Navigation Properties
    public virtual ICollection<AwardVerificationChecklistItem> Items { get; set; } = new List<AwardVerificationChecklistItem>();
}

/// <summary>
/// Individual checklist items within a template
/// </summary>
public class AwardVerificationChecklistItem : TenantEntity
{
    [Required]
    public Guid TemplateId { get; set; }

    [Required]
    [MaxLength(200)]
    public string ItemText { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    /// <summary>
    /// Order in which items should be displayed
    /// </summary>
    public int DisplayOrder { get; set; } = 0;

    /// <summary>
    /// Whether this item must be verified before award can proceed
    /// </summary>
    public bool IsRequired { get; set; } = true;

    /// <summary>
    /// Category for grouping items (e.g., "Financial", "Legal", "Technical")
    /// </summary>
    [MaxLength(50)]
    public string? Category { get; set; }

    public bool IsActive { get; set; } = true;

    // Navigation Properties
    public virtual AwardVerificationChecklistTemplate Template { get; set; } = null!;
}

/// <summary>
/// Award verification record for a tender - links selected bidders to verification checklist
/// </summary>
public class TenderAwardVerification : TenantEntity
{
    [Required]
    public Guid TenderId { get; set; }

    /// <summary>
    /// The template used for this verification
    /// </summary>
    public Guid? TemplateId { get; set; }

    [Required]
    [MaxLength(50)]
    public string Status { get; set; } = "Pending"; // Pending, InProgress, Completed, Cancelled

    public DateTime? StartedDate { get; set; }
    public DateTime? CompletedDate { get; set; }

    public Guid? StartedById { get; set; }
    public Guid? CompletedById { get; set; }

    public string? Notes { get; set; }

    // Navigation Properties
    public virtual Tender Tender { get; set; } = null!;
    public virtual AwardVerificationChecklistTemplate? Template { get; set; }
    public virtual ApplicationUser? StartedBy { get; set; }
    public virtual ApplicationUser? CompletedBy { get; set; }
    public virtual ICollection<TenderAwardVerificationBidder> Bidders { get; set; } = new List<TenderAwardVerificationBidder>();
}

/// <summary>
/// Selected bidders for verification
/// </summary>
public class TenderAwardVerificationBidder : TenantEntity
{
    [Required]
    public Guid VerificationId { get; set; }

    [Required]
    public Guid TenderBidId { get; set; }

    [Required]
    public Guid BusinessPartnerId { get; set; }

    [Required]
    [MaxLength(50)]
    public string Status { get; set; } = "Pending"; // Pending, Verified, Failed

    public DateTime? VerifiedDate { get; set; }
    public Guid? VerifiedById { get; set; }

    public string? OverallComments { get; set; }

    // Navigation Properties
    public virtual TenderAwardVerification Verification { get; set; } = null!;
    public virtual TenderBid TenderBid { get; set; } = null!;
    public virtual BusinessPartner BusinessPartner { get; set; } = null!;
    public virtual ApplicationUser? VerifiedBy { get; set; }
    public virtual ICollection<TenderAwardVerificationItemResult> ItemResults { get; set; } = new List<TenderAwardVerificationItemResult>();
}

/// <summary>
/// Individual verification item result for a bidder
/// </summary>
public class TenderAwardVerificationItemResult : TenantEntity
{
    [Required]
    public Guid BidderId { get; set; }

    [Required]
    public Guid ChecklistItemId { get; set; }

    /// <summary>
    /// Whether this item has been verified
    /// </summary>
    public bool IsVerified { get; set; } = false;

    /// <summary>
    /// Verification status: Pending, Passed, Failed, NotApplicable
    /// </summary>
    [Required]
    [MaxLength(50)]
    public string Status { get; set; } = "Pending";

    /// <summary>
    /// Comments from the verifier
    /// </summary>
    public string? Comments { get; set; }

    public DateTime? VerifiedDate { get; set; }
    public Guid? VerifiedById { get; set; }

    // Navigation Properties
    public virtual TenderAwardVerificationBidder Bidder { get; set; } = null!;
    public virtual AwardVerificationChecklistItem ChecklistItem { get; set; } = null!;
    public virtual ApplicationUser? VerifiedBy { get; set; }
    public virtual ICollection<TenderAwardVerificationItemDocument> Documents { get; set; } = new List<TenderAwardVerificationItemDocument>();
}

/// <summary>
/// Document attachments for verification item results (supporting documents, certificates, etc.)
/// </summary>
public class TenderAwardVerificationItemDocument : TenantEntity
{
    [Required]
    public Guid ItemResultId { get; set; }

    [Required]
    [MaxLength(255)]
    public string FileName { get; set; } = string.Empty;

    [Required]
    [MaxLength(500)]
    public string FilePath { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? ContentType { get; set; }

    public long FileSize { get; set; }

    /// <summary>
    /// Document type: Certificate, License, Report, Photo, Other
    /// </summary>
    [MaxLength(50)]
    public string DocumentType { get; set; } = "General";

    [MaxLength(500)]
    public string? Description { get; set; }

    public Guid UploadedById { get; set; }
    public DateTime UploadedDate { get; set; } = DateTime.UtcNow;

    // Navigation Properties
    [ForeignKey(nameof(ItemResultId))]
    public virtual TenderAwardVerificationItemResult ItemResult { get; set; } = null!;

    [ForeignKey(nameof(UploadedById))]
    public virtual ApplicationUser UploadedBy { get; set; } = null!;
}

