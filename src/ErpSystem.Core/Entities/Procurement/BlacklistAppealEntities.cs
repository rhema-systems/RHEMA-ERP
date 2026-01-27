using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Entities;

namespace ErpSystem.Core.Entities.Procurement;

/// <summary>
/// Blacklist appeal entity for partners to appeal their blacklist status
/// </summary>
public class BlacklistAppeal : TenantEntity
{
    [Required]
    public Guid BusinessPartnerId { get; set; }

    [Required]
    [MaxLength(50)]
    public string AppealNumber { get; set; } = string.Empty;

    public DateTime AppealDate { get; set; } = DateTime.UtcNow;

    [Required]
    [MaxLength(50)]
    public string Status { get; set; } = "Pending"; // Pending, UnderReview, Approved, Rejected

    [Required]
    public string AppealReason { get; set; } = string.Empty;

    public string? SupportingDocuments { get; set; } // JSON array of document paths

    public string? CorrectiveActionsTaken { get; set; }

    public string? PreventiveMeasures { get; set; }

    // Review Information
    public Guid? ReviewedById { get; set; }
    public DateTime? ReviewedDate { get; set; }

    public string? ReviewerComments { get; set; }

    public string? RejectionReason { get; set; }

    // Decision Information
    public Guid? ApprovedById { get; set; }
    public DateTime? ApprovedDate { get; set; }

    public bool? RemoveBlacklist { get; set; } // If approved, should blacklist be removed?

    public DateTime? NewBlacklistExpiryDate { get; set; } // If approved, new expiry date

    public string? DecisionNotes { get; set; }

    // Navigation Properties
    public virtual BusinessPartner BusinessPartner { get; set; } = null!;
    public virtual ApplicationUser? ReviewedBy { get; set; }
    public virtual ApplicationUser? ApprovedBy { get; set; }
}

/// <summary>
/// Blacklist history for audit trail
/// </summary>
public class BlacklistHistory : TenantEntity
{
    [Required]
    public Guid BusinessPartnerId { get; set; }

    [Required]
    [MaxLength(50)]
    public string Action { get; set; } = string.Empty; // Added, Removed, AppealApproved, AppealRejected, Expired

    public DateTime ActionDate { get; set; } = DateTime.UtcNow;

    public Guid? ActionById { get; set; }

    [MaxLength(200)]
    public string? ActionByName { get; set; }

    public string? Reason { get; set; }

    public DateTime? BlacklistDate { get; set; }

    public DateTime? BlacklistExpiryDate { get; set; }

    public Guid? RelatedAppealId { get; set; }

    public string? Notes { get; set; }

    // Navigation Properties
    public virtual BusinessPartner BusinessPartner { get; set; } = null!;
    public virtual ApplicationUser? ActionBy { get; set; }
    public virtual BlacklistAppeal? RelatedAppeal { get; set; }
}

