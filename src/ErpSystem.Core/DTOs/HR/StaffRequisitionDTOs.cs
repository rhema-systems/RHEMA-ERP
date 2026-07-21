using System.ComponentModel.DataAnnotations;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.DTOs.HR;

// ============================================================================
// STAFF REQUISITION DTOs
// ============================================================================

#region Staff Requisition

public class StaffRequisitionDto : BaseDto
{
    public Guid TenantId { get; set; }
    public string RequisitionNumber { get; set; } = string.Empty;
    public string? RequisitionTitle { get; set; }
    public string? Description { get; set; }

    // Location & Organisation
    public Guid? LocationLevelId { get; set; }
    public string? LocationLevelName { get; set; }
    public Guid? LocationId { get; set; }
    public string? LocationName { get; set; }
    public Guid? OrganizationLevelId { get; set; }
    public string? OrganizationLevelName { get; set; }
    public Guid? OrganizationUnitId { get; set; }
    public string? OrganizationUnitName { get; set; }

    // Position & Job Description
    public Guid PositionId { get; set; }
    public string PositionTitle { get; set; } = string.Empty;
    public Guid? JobDescriptionId { get; set; }
    public string? JobDescriptionTitle { get; set; }

    // Classification
    public StaffRequisitionType Type { get; set; }
    public string TypeName => Type.ToString();
    public StaffRequisitionPriority Priority { get; set; }
    public string PriorityName => Priority.ToString();
    public StaffRequisitionStatus Status { get; set; }
    public string StatusName => Status.ToString();

    // Headcount
    public int NumberOfPositions { get; set; }
    public int PositionsFilled { get; set; }
    public int PositionsRemaining => NumberOfPositions - PositionsFilled;

    // Replacement
    public Guid? ReplacementForEmployeeId { get; set; }
    public string? ReplacementForEmployeeName { get; set; }
    public StaffReplacementReason? ReplacementReason { get; set; }
    public string? ReplacementReasonName => ReplacementReason?.ToString();
    public DateTime? EmployeeDepartureDate { get; set; }

    // Timeline
    public DateTime RequestDate { get; set; }
    public DateTime DesiredStartDate { get; set; }
    public DateTime? LatestAcceptableStartDate { get; set; }
    public DateTime? TargetFillDate { get; set; }
    public DateTime? ExpectedOfferDate { get; set; }
    public int? DaysToFill { get; set; }
    public string? TargetStartDateReason { get; set; }

    // Justification
    public string BusinessJustification { get; set; } = string.Empty;
    public string? ImpactIfNotFilled { get; set; }

    // Budget
    public bool IsBudgeted { get; set; }
    public string? BudgetCode { get; set; }

    // Recruitment Strategy
    public bool AllowInternalCandidates { get; set; }
    public bool AllowExternalCandidates { get; set; }

    // Requestor
    public Guid RequestedById { get; set; }
    public string RequestedByName { get; set; } = string.Empty;

    // Fulfillment
    public bool IsFulfilled { get; set; }
    public DateTime? FulfilledDate { get; set; }

    // Cancellation
    public Guid? CancelledById { get; set; }
    public string? CancelledByName { get; set; }
    public DateTime? CancelledDate { get; set; }
    public string? CancellationReason { get; set; }

    // Workflow & Vacancy
    public Guid? WorkflowInstanceId { get; set; }
    public Guid? JobVacancyId { get; set; }
    public string? JobVacancyNumber { get; set; }

    public string Notes { get; set; } = string.Empty;
}

public class StaffRequisitionSummaryDto
{
    public Guid Id { get; set; }
    public string RequisitionNumber { get; set; } = string.Empty;
    public string? RequisitionTitle { get; set; }
    public string PositionTitle { get; set; } = string.Empty;
    public string? OrganizationUnitName { get; set; }
    public string? LocationName { get; set; }
    public StaffRequisitionType Type { get; set; }
    public string TypeName => Type.ToString();
    public StaffRequisitionPriority Priority { get; set; }
    public string PriorityName => Priority.ToString();
    public StaffRequisitionStatus Status { get; set; }
    public string StatusName => Status.ToString();
    public int NumberOfPositions { get; set; }
    public int PositionsFilled { get; set; }
    public int PositionsRemaining => NumberOfPositions - PositionsFilled;
    public DateTime RequestDate { get; set; }
    public DateTime DesiredStartDate { get; set; }
    public string RequestedByName { get; set; } = string.Empty;
}

public class StaffRequisitionDetailDto : StaffRequisitionDto
{
    public List<StaffRequisitionCostDto> Costs { get; set; } = new();
    public List<StaffRequisitionAttachmentDto> Attachments { get; set; } = new();
    public List<StaffRequisitionCommentDto> Comments { get; set; } = new();
    public List<StaffRequisitionHistoryDto> History { get; set; } = new();
}

public class StaffRequisitionStatusSummaryDto
{
    public int TotalRequisitions  { get; set; }
    public int Draft              { get; set; }
    public int Submitted          { get; set; }
    public int UnderReview        { get; set; }
    public int Approved           { get; set; }
    public int Rejected           { get; set; }
    public int OnHold             { get; set; }
    public int Cancelled          { get; set; }
    public int PartiallyFulfilled { get; set; }
    public int Fulfilled          { get; set; }
}

public class CreateStaffRequisitionDto : CreateDtoBase
{
    [Required]
    public Guid PositionId { get; set; }

    public Guid? JobDescriptionId { get; set; }

    public Guid? LocationLevelId { get; set; }
    public Guid? LocationId { get; set; }
    public Guid? OrganizationLevelId { get; set; }
    public Guid? OrganizationUnitId { get; set; }

    [Required]
    public StaffRequisitionType Type { get; set; }

    public StaffRequisitionPriority Priority { get; set; } = StaffRequisitionPriority.Medium;

    [Required]
    [MaxLength(200)]
    public string? RequisitionTitle { get; set; }

    [MaxLength(500)]
    public string? Description { get; set; }

    [Required]
    [Range(1, 100)]
    public int NumberOfPositions { get; set; } = 1;

    // Replacement
    public Guid? ReplacementForEmployeeId { get; set; }
    public StaffReplacementReason? ReplacementReason { get; set; }
    public DateTime? EmployeeDepartureDate { get; set; }

    // Timeline
    [Required]
    public DateTime DesiredStartDate { get; set; }
    public DateTime? LatestAcceptableStartDate { get; set; }
    public DateTime? TargetFillDate { get; set; }
    public DateTime? ExpectedOfferDate { get; set; }

    [MaxLength(1000)]
    public string? TargetStartDateReason { get; set; }

    // Justification
    [Required]
    [MaxLength(2000)]
    public string BusinessJustification { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? ImpactIfNotFilled { get; set; }

    // Budget
    public bool IsBudgeted { get; set; }

    [MaxLength(50)]
    public string? BudgetCode { get; set; }

    // Recruitment Strategy
    public bool AllowInternalCandidates { get; set; } = true;
    public bool AllowExternalCandidates { get; set; } = true;

    [MaxLength(1000)]
    public string Notes { get; set; } = string.Empty;
}

public class UpdateStaffRequisitionDto : UpdateDtoBase
{
    [Required]
    public Guid PositionId { get; set; }

    public Guid? JobDescriptionId { get; set; }

    public Guid? LocationLevelId { get; set; }
    public Guid? LocationId { get; set; }
    public Guid? OrganizationLevelId { get; set; }
    public Guid? OrganizationUnitId { get; set; }

    [Required]
    public StaffRequisitionType Type { get; set; }

    public StaffRequisitionPriority Priority { get; set; }

    [MaxLength(200)]
    public string? RequisitionTitle { get; set; }

    [MaxLength(500)]
    public string? Description { get; set; }

    [Required]
    [Range(1, 100)]
    public int NumberOfPositions { get; set; }

    // Replacement
    public Guid? ReplacementForEmployeeId { get; set; }
    public StaffReplacementReason? ReplacementReason { get; set; }
    public DateTime? EmployeeDepartureDate { get; set; }

    // Timeline
    [Required]
    public DateTime DesiredStartDate { get; set; }
    public DateTime? LatestAcceptableStartDate { get; set; }
    public DateTime? TargetFillDate { get; set; }
    public DateTime? ExpectedOfferDate { get; set; }

    [MaxLength(1000)]
    public string? TargetStartDateReason { get; set; }

    // Justification
    [Required]
    [MaxLength(2000)]
    public string BusinessJustification { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? ImpactIfNotFilled { get; set; }

    // Budget
    public bool IsBudgeted { get; set; }

    [MaxLength(50)]
    public string? BudgetCode { get; set; }

    // Recruitment Strategy
    public bool AllowInternalCandidates { get; set; }
    public bool AllowExternalCandidates { get; set; }

    [MaxLength(1000)]
    public string Notes { get; set; } = string.Empty;
}

public class SubmitStaffRequisitionDto
{
    [Required]
    public Guid RequisitionId { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }
}

public class ApproveStaffRequisitionDto
{
    [Required]
    public Guid RequisitionId { get; set; }

    [MaxLength(1000)]
    public string? Comments { get; set; }
}

public class RejectStaffRequisitionDto
{
    [Required]
    public Guid RequisitionId { get; set; }

    [Required]
    [MaxLength(1000)]
    public string Comments { get; set; } = string.Empty;
}

public class HoldStaffRequisitionDto
{
    [Required]
    public Guid RequisitionId { get; set; }

    [Required]
    [MaxLength(1000)]
    public string Reason { get; set; } = string.Empty;
}

public class CancelStaffRequisitionDto
{
    [Required]
    public Guid RequisitionId { get; set; }

    [Required]
    [MaxLength(1000)]
    public string CancellationReason { get; set; } = string.Empty;
}

public class FulfillStaffRequisitionDto
{
    [Required]
    public Guid RequisitionId { get; set; }

    [Required]
    [Range(1, 100)]
    public int PositionsFilled { get; set; }

    public DateTime? FulfilledDate { get; set; }
}

public class LinkStaffRequisitionToVacancyDto
{
    [Required]
    public Guid RequisitionId { get; set; }

    [Required]
    public Guid JobVacancyId { get; set; }
}

#endregion

// ============================================================================
// STAFF REQUISITION COST DTOs
// ============================================================================

#region Staff Requisition Cost

public class StaffRequisitionCostDto : BaseDto
{
    public Guid TenantId { get; set; }
    public Guid RequisitionId { get; set; }
    public string RequisitionNumber { get; set; } = string.Empty;

    public StaffRequisitionCostCategory Category { get; set; }
    public string CategoryName => Category.ToString();

    public string Purpose { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "GHS";
    public decimal ExchangeRate { get; set; }
    public decimal AmountInBaseCurrency => Amount * ExchangeRate;
    public string? Description { get; set; }
    public string? PaymentVoucherNumber { get; set; }

    public Guid RecordedById { get; set; }
    public string RecordedByName { get; set; } = string.Empty;
    public DateTime RecordedDate { get; set; }
}

public class CreateStaffRequisitionCostDto : CreateDtoBase
{
    [Required]
    public Guid RequisitionId { get; set; }

    [Required]
    public StaffRequisitionCostCategory Category { get; set; }

    [Required]
    [MaxLength(100)]
    public string Purpose { get; set; } = string.Empty;

    [Required]
    [Range(0.01, double.MaxValue)]
    public decimal Amount { get; set; }

    [MaxLength(3)]
    public string Currency { get; set; } = "GHS";

    [Range(0.000001, double.MaxValue)]
    public decimal ExchangeRate { get; set; } = 1;

    [MaxLength(500)]
    public string? Description { get; set; }

    [MaxLength(50)]
    public string? PaymentVoucherNumber { get; set; }
}

public class UpdateStaffRequisitionCostDto : UpdateDtoBase
{
    [Required]
    public StaffRequisitionCostCategory Category { get; set; }

    [Required]
    [MaxLength(100)]
    public string Purpose { get; set; } = string.Empty;

    [Required]
    [Range(0.01, double.MaxValue)]
    public decimal Amount { get; set; }

    [MaxLength(3)]
    public string Currency { get; set; } = "GHS";

    [Range(0.000001, double.MaxValue)]
    public decimal ExchangeRate { get; set; } = 1;

    [MaxLength(500)]
    public string? Description { get; set; }

    [MaxLength(50)]
    public string? PaymentVoucherNumber { get; set; }
}

#endregion

// ============================================================================
// STAFF REQUISITION ATTACHMENT DTOs
// ============================================================================

#region Staff Requisition Attachment

public class StaffRequisitionAttachmentDto : BaseDto
{
    public Guid TenantId { get; set; }
    public Guid RequisitionId { get; set; }
    public string RequisitionNumber { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime UploadDate { get; set; }
    public Guid UploadedById { get; set; }
    public string UploadedByName { get; set; } = string.Empty;
}

public class CreateStaffRequisitionAttachmentDto : CreateDtoBase
{
    [Required]
    public Guid RequisitionId { get; set; }

    [Required]
    [MaxLength(200)]
    public string FileName { get; set; } = string.Empty;

    [Required]
    [MaxLength(500)]
    public string FilePath { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }
}

#endregion

// ============================================================================
// STAFF REQUISITION COMMENT DTOs
// ============================================================================

#region Staff Requisition Comment

public class StaffRequisitionCommentDto : BaseDto
{
    public Guid TenantId { get; set; }
    public Guid RequisitionId { get; set; }
    public Guid? ParentCommentId { get; set; }
    public string Body { get; set; } = string.Empty;
    public Guid AuthorId { get; set; }
    public string AuthorName { get; set; } = string.Empty;
    public DateTime PostedDate { get; set; }
    public List<StaffRequisitionCommentDto> Replies { get; set; } = new();
}

public class CreateStaffRequisitionCommentDto : CreateDtoBase
{
    [Required]
    public Guid RequisitionId { get; set; }

    public Guid? ParentCommentId { get; set; }

    [Required]
    [MaxLength(3000)]
    public string Body { get; set; } = string.Empty;
}

public class UpdateStaffRequisitionCommentDto : UpdateDtoBase
{
    [Required]
    [MaxLength(3000)]
    public string Body { get; set; } = string.Empty;
}

#endregion

// ============================================================================
// STAFF REQUISITION HISTORY DTOs
// ============================================================================

#region Staff Requisition History

public class StaffRequisitionHistoryDto : BaseDto
{
    public Guid TenantId { get; set; }
    public Guid RequisitionId { get; set; }
    public string RequisitionNumber { get; set; } = string.Empty;
    public StaffRequisitionStatus FromStatus { get; set; }
    public string FromStatusName => FromStatus.ToString();
    public StaffRequisitionStatus ToStatus { get; set; }
    public string ToStatusName => ToStatus.ToString();
    public Guid ChangedById { get; set; }
    public string ChangedByName { get; set; } = string.Empty;
    public string? Comments { get; set; }
    public DateTime ActionDate { get; set; }
}

#endregion


#region Budget Check

/// <summary>
/// Result of checking a staff requisition against the position's approved manpower budget line
/// for the fiscal year. Drives the advisory warning banner on the requisition UI and the
/// hard-stop enforcement on submit/approve. When <see cref="HasBudgetLine"/> is false there is
/// no budgeted headcount for the position, so enforcement never applies (hiring is never blocked
/// for an unbudgeted position).
/// </summary>
public class RequisitionBudgetCheckDto
{
    /// <summary>Tenant policy: how strictly the budget is enforced (Off / Warn / Block).</summary>
    public BudgetEnforcementMode Mode { get; set; }
    public string ModeName => Mode.ToString();

    /// <summary>Fiscal year the check was resolved against (from the requisition's desired start date).</summary>
    public int FiscalYear { get; set; }

    /// <summary>True when a manpower budget line exists for this position + fiscal year.</summary>
    public bool HasBudgetLine { get; set; }

    /// <summary>Approved planned headcount for the position (from the budget line). Null when none exists.</summary>
    public int? PlannedCount { get; set; }

    /// <summary>Headcount already recorded as filled against the budget line. Null when none exists.</summary>
    public int? CurrentFilled { get; set; }

    /// <summary>Number of positions this requisition is requesting.</summary>
    public int RequestedPositions { get; set; }

    /// <summary>CurrentFilled + RequestedPositions — the headcount the position would reach if fulfilled.</summary>
    public int ProjectedHeadcount { get; set; }

    /// <summary>True when the projected headcount exceeds the approved planned count.</summary>
    public bool IsOverBudget { get; set; }

    /// <summary>True when this over-budget state would block submit/approve under the current mode.</summary>
    public bool WouldBlock { get; set; }

    /// <summary>Human-readable explanation for the UI / API error message.</summary>
    public string Message { get; set; } = string.Empty;
}

#endregion
