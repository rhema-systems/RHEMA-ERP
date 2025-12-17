using System.ComponentModel.DataAnnotations;

namespace ErpSystem.Core.DTOs.Procurement;

/// <summary>
/// DTO for blacklist appeal
/// </summary>
public class BlacklistAppealDto
{
    public Guid Id { get; set; }
    public Guid BusinessPartnerId { get; set; }
    public string? PartnerName { get; set; }
    public string? PartnerCode { get; set; }
    public string AppealNumber { get; set; } = string.Empty;
    public DateTime AppealDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public string AppealReason { get; set; } = string.Empty;
    public string? SupportingDocuments { get; set; }
    public string? CorrectiveActionsTaken { get; set; }
    public string? PreventiveMeasures { get; set; }
    
    public Guid? ReviewedById { get; set; }
    public string? ReviewedByName { get; set; }
    public DateTime? ReviewedDate { get; set; }
    public string? ReviewerComments { get; set; }
    public string? RejectionReason { get; set; }
    
    public Guid? ApprovedById { get; set; }
    public string? ApprovedByName { get; set; }
    public DateTime? ApprovedDate { get; set; }
    public bool? RemoveBlacklist { get; set; }
    public DateTime? NewBlacklistExpiryDate { get; set; }
    public string? DecisionNotes { get; set; }
    
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// DTO for creating blacklist appeal
/// </summary>
public class CreateBlacklistAppealDto
{
    [Required]
    public Guid BusinessPartnerId { get; set; }

    [Required]
    [MinLength(10)]
    public string AppealReason { get; set; } = string.Empty;

    public string? SupportingDocuments { get; set; }

    public string? CorrectiveActionsTaken { get; set; }

    public string? PreventiveMeasures { get; set; }
}

/// <summary>
/// DTO for reviewing blacklist appeal
/// </summary>
public class ReviewBlacklistAppealDto
{
    [Required]
    public string ReviewerComments { get; set; } = string.Empty;
}

/// <summary>
/// DTO for approving blacklist appeal
/// </summary>
public class ApproveBlacklistAppealDto
{
    [Required]
    public bool RemoveBlacklist { get; set; }

    public DateTime? NewBlacklistExpiryDate { get; set; }

    public string? DecisionNotes { get; set; }
}

/// <summary>
/// DTO for rejecting blacklist appeal
/// </summary>
public class RejectBlacklistAppealDto
{
    [Required]
    [MinLength(10)]
    public string RejectionReason { get; set; } = string.Empty;

    public string? DecisionNotes { get; set; }
}

/// <summary>
/// DTO for blacklist history
/// </summary>
public class BlacklistHistoryDto
{
    public Guid Id { get; set; }
    public Guid BusinessPartnerId { get; set; }
    public string? PartnerName { get; set; }
    public string Action { get; set; } = string.Empty;
    public DateTime ActionDate { get; set; }
    public Guid? ActionById { get; set; }
    public string? ActionByName { get; set; }
    public string? Reason { get; set; }
    public DateTime? BlacklistDate { get; set; }
    public DateTime? BlacklistExpiryDate { get; set; }
    public Guid? RelatedAppealId { get; set; }
    public string? RelatedAppealNumber { get; set; }
    public string? Notes { get; set; }
}

