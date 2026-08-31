using System.ComponentModel.DataAnnotations;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.DTOs.HR;

// ============================================================================
// STAFF MOVEMENT DTOs
// ============================================================================

#region Staff Movement

/// <summary>
/// Full read model for a staff movement record.
/// </summary>
public class StaffMovementDto : BaseDto
{
    public Guid TenantId { get; set; }
    public string MovementNumber { get; set; } = string.Empty;

    // ── Employee ──────────────────────────────────────────────────────────────
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public string? EmployeeNumber { get; set; }

    // ── Classification ────────────────────────────────────────────────────────
    public StaffMovementType MovementType { get; set; }
    public string MovementTypeName => MovementType.ToString();
    public StaffMovementCategory Category { get; set; }
    public string CategoryName => Category.ToString();

    // ── Current Position (Before Movement) ───────────────────────────────────
    public Guid CurrentPositionId { get; set; }
    public string CurrentPositionTitle { get; set; } = string.Empty;

    public Guid CurrentOrganizationUnitId { get; set; }
    public string CurrentOrganizationUnitName { get; set; } = string.Empty;
    public Guid? CurrentOrganizationLevelId { get; set; }
    public string? CurrentOrganizationLevelName { get; set; }

    public Guid? CurrentLocationId { get; set; }
    public string? CurrentLocationName { get; set; }
    public Guid? CurrentLocationLevelId { get; set; }
    public string? CurrentLocationLevelName { get; set; }

    public Guid? CurrentSupervisorId { get; set; }
    public string? CurrentSupervisorName { get; set; }

    // ── Current Salary & Grade ────────────────────────────────────────────────
    public decimal CurrentSalary { get; set; }
    public Guid? CurrentSalaryGradeId { get; set; }
    public string? CurrentSalaryGradeCode { get; set; }
    public string? CurrentSalaryGradeName { get; set; }
    public Guid? CurrentSalaryLevelId { get; set; }
    public string? CurrentSalaryLevelName { get; set; }
    public Guid? CurrentSalaryNotchId { get; set; }
    public int? CurrentSalaryNotchNumber { get; set; }

    // ── New Position (After Movement) ─────────────────────────────────────────
    public Guid NewPositionId { get; set; }
    public string NewPositionTitle { get; set; } = string.Empty;

    public Guid NewOrganizationUnitId { get; set; }
    public string NewOrganizationUnitName { get; set; } = string.Empty;
    public Guid? NewOrganizationLevelId { get; set; }
    public string? NewOrganizationLevelName { get; set; }

    public Guid? NewLocationId { get; set; }
    public string? NewLocationName { get; set; }
    public Guid? NewLocationLevelId { get; set; }
    public string? NewLocationLevelName { get; set; }

    public Guid? NewSupervisorId { get; set; }
    public string? NewSupervisorName { get; set; }

    // ── New Salary & Grade ────────────────────────────────────────────────────
    public decimal NewSalary { get; set; }
    public Guid? NewSalaryGradeId { get; set; }
    public string? NewSalaryGradeCode { get; set; }
    public string? NewSalaryGradeName { get; set; }
    public Guid? NewSalaryLevelId { get; set; }
    public string? NewSalaryLevelName { get; set; }
    public Guid? NewSalaryNotchId { get; set; }
    public int? NewSalaryNotchNumber { get; set; }

    public decimal? SalaryIncreaseAmount { get; set; }
    public decimal? SalaryIncreasePercentage { get; set; }

    // ── Reason & Justification ────────────────────────────────────────────────
    public string Reason { get; set; } = string.Empty;
    public string? Justification { get; set; }
    public bool IsReorganization { get; set; }
    public bool IsSuccessionPlan { get; set; }
    public Guid? SuccessionPlanId { get; set; }
    public string? SuccessionPlanNumber { get; set; }

    // ── Dates ─────────────────────────────────────────────────────────────────
    public DateTime RequestDate { get; set; }
    public DateTime EffectiveDate { get; set; }

    // ── Temporary Movement ────────────────────────────────────────────────────
    public bool IsTemporary { get; set; }
    public DateTime? TemporaryEndDate { get; set; }
    public string? TemporaryArrangementDetails { get; set; }
    public bool ReturnProcessed { get; set; }
    public DateTime? ActualReturnDate { get; set; }
    public Guid? ReturnMovementId { get; set; }
    public string? ReturnMovementNumber { get; set; }

    // ── Requester ─────────────────────────────────────────────────────────────
    public Guid RequestedById { get; set; }
    public string RequestedByName { get; set; } = string.Empty;
    public DateTime RequestSubmissionDate { get; set; }

    // ── Authorization ─────────────────────────────────────────────────────────
    public Guid? AuthorizedById { get; set; }
    public string? AuthorizedByName { get; set; }
    public DateTime? AuthorizationDate { get; set; }

    // ── Status ────────────────────────────────────────────────────────────────
    public StaffMovementStatus Status { get; set; }
    public string StatusName => Status.ToString();

    // ── Rejection ─────────────────────────────────────────────────────────────
    public string? RejectionReason { get; set; }
    public Guid? RejectedById { get; set; }
    public string? RejectedByName { get; set; }
    public DateTime? RejectionDate { get; set; }

    // ── Cancellation ──────────────────────────────────────────────────────────
    public string? CancellationReason { get; set; }
    public Guid? CancelledById { get; set; }
    public string? CancelledByName { get; set; }
    public DateTime? CancellationDate { get; set; }

    // ── Employee Acceptance ───────────────────────────────────────────────────
    public bool RequiresEmployeeAcceptance { get; set; }
    public bool? EmployeeAccepted { get; set; }
    public DateTime? EmployeeResponseDate { get; set; }
    public string? EmployeeComments { get; set; }

    // ── Handover ──────────────────────────────────────────────────────────────
    public bool RequiresHandover { get; set; }
    public DateTime? HandoverCompletionDate { get; set; }
    public string? HandoverNotes { get; set; }

    // ── Performance Basis ─────────────────────────────────────────────────────
    public Guid? BasedOnAppraisalId { get; set; }
    public string? BasedOnAppraisalNumber { get; set; }

    public string? AdditionalNotes { get; set; }
}

/// <summary>
/// Lightweight list-view model for movement grids and dashboards.
/// </summary>
public class StaffMovementSummaryDto
{
    public Guid Id { get; set; }
    public string MovementNumber { get; set; } = string.Empty;
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public string? EmployeeNumber { get; set; }
    public StaffMovementType MovementType { get; set; }
    public string MovementTypeName => MovementType.ToString();
    public StaffMovementCategory Category { get; set; }
    public string CategoryName => Category.ToString();
    public string CurrentPositionTitle { get; set; } = string.Empty;
    public string CurrentOrganizationUnitName { get; set; } = string.Empty;
    public string NewPositionTitle { get; set; } = string.Empty;
    public string NewOrganizationUnitName { get; set; } = string.Empty;
    public DateTime RequestDate { get; set; }
    public DateTime? EffectiveDate { get; set; }
    public StaffMovementStatus Status { get; set; }
    public string StatusName => Status.ToString();
    public bool IsTemporary { get; set; }
    public DateTime? TemporaryEndDate { get; set; }
    public bool ReturnProcessed { get; set; }

    /// <summary>Populated when <see cref="MovementType"/> is Promotion and promotion detail is loaded.</summary>
    public StaffPromotionType? PromotionType { get; set; }

    /// <summary>Grade bands increased (promotion movements only).</summary>
    public int? GradeLevelIncrease { get; set; }

    /// <summary>True when the promotion is an acting/temporary elevation.</summary>
    public bool IsActingPromotion { get; set; }

    /// <summary>Populated when <see cref="MovementType"/> is Transfer and transfer detail is loaded.</summary>
    public StaffTransferType? TransferType { get; set; }

    /// <summary>True when relocation assistance applies (transfer movements only).</summary>
    public bool RequiresRelocation { get; set; }

    /// <summary>True for inter-company / legal-entity transfers.</summary>
    public bool IsInterCompany { get; set; }

    /// <summary>True when today falls within the transfer transition window.</summary>
    public bool IsInTransition { get; set; }
}

/// <summary>Request body for batch summary lookup by parent movement IDs.</summary>
public class StaffMovementSummariesByIdsRequest
{
    public List<Guid> Ids { get; set; } = new();
}

/// <summary>
/// Full detail model including all child collections and subtype detail.
/// </summary>
public class StaffMovementDetailDto : StaffMovementDto
{
    public List<StaffMovementApprovalLevelDto> ApprovalLevels { get; set; } = new();
    public List<StaffMovementStatusHistoryDto> StatusHistory { get; set; } = new();
    public List<StaffMovementAttachmentDto> Attachments { get; set; } = new();
    public List<StaffMovementChecklistItemDto> ChecklistItems { get; set; } = new();

    // Subtype detail — at most one will be non-null per movement
    public StaffPromotionDto? Promotion { get; set; }
    public StaffTransferDto? Transfer { get; set; }
    public StaffDemotionDto? Demotion { get; set; }
    public StaffSecondmentDto? Secondment { get; set; }
}

public class CreateStaffMovementDto : CreateDtoBase
{
    [Required]
    public Guid EmployeeId { get; set; }

    [Required]
    public StaffMovementType MovementType { get; set; }

    [Required]
    public StaffMovementCategory Category { get; set; }

    // ── Current Position (snapshot before the movement) ───────────────────────
    [Required]
    public Guid CurrentPositionId { get; set; }

    [Required]
    public Guid CurrentOrganizationUnitId { get; set; }

    public Guid? CurrentOrganizationLevelId { get; set; }
    public Guid? CurrentLocationId { get; set; }
    public Guid? CurrentLocationLevelId { get; set; }
    public Guid? CurrentSupervisorId { get; set; }

    // ── Current Salary & Grade (snapshot) ────────────────────────────────────
    [Range(0, double.MaxValue)]
    public decimal CurrentSalary { get; set; }

    public Guid? CurrentSalaryGradeId { get; set; }
    public Guid? CurrentSalaryLevelId { get; set; }
    public Guid? CurrentSalaryNotchId { get; set; }

    // ── New Position ──────────────────────────────────────────────────────────
    [Required]
    public Guid NewPositionId { get; set; }

    [Required]
    public Guid NewOrganizationUnitId { get; set; }

    public Guid? NewOrganizationLevelId { get; set; }
    public Guid? NewLocationId { get; set; }
    public Guid? NewLocationLevelId { get; set; }
    public Guid? NewSupervisorId { get; set; }

    // ── New Salary & Grade ────────────────────────────────────────────────────
    [Required]
    [Range(0, double.MaxValue)]
    public decimal NewSalary { get; set; }

    public Guid? NewSalaryGradeId { get; set; }
    public Guid? NewSalaryLevelId { get; set; }
    public Guid? NewSalaryNotchId { get; set; }

    // ── Reason ────────────────────────────────────────────────────────────────
    [Required]
    [MaxLength(500)]
    public string Reason { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Justification { get; set; }

    public bool IsReorganization { get; set; }
    public bool IsSuccessionPlan { get; set; }
    public Guid? SuccessionPlanId { get; set; }

    [Required]
    public DateTime EffectiveDate { get; set; }

    public bool IsTemporary { get; set; }
    public DateTime? TemporaryEndDate { get; set; }

    [MaxLength(1000)]
    public string? TemporaryArrangementDetails { get; set; }

    public bool RequiresEmployeeAcceptance { get; set; }
    public bool RequiresHandover { get; set; }

    public Guid? BasedOnAppraisalId { get; set; }

    [MaxLength(2000)]
    public string? AdditionalNotes { get; set; }
}

public class UpdateStaffMovementDto : UpdateDtoBase
{
    [Required]
    public Guid NewPositionId { get; set; }

    [Required]
    public Guid NewOrganizationUnitId { get; set; }

    public Guid? NewOrganizationLevelId { get; set; }
    public Guid? NewLocationId { get; set; }
    public Guid? NewLocationLevelId { get; set; }
    public Guid? NewSupervisorId { get; set; }

    [Required]
    [Range(0, double.MaxValue)]
    public decimal NewSalary { get; set; }

    public Guid? NewSalaryGradeId { get; set; }
    public Guid? NewSalaryLevelId { get; set; }
    public Guid? NewSalaryNotchId { get; set; }

    [Required]
    [MaxLength(500)]
    public string Reason { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Justification { get; set; }

    [Required]
    public DateTime EffectiveDate { get; set; }

    public bool IsTemporary { get; set; }
    public DateTime? TemporaryEndDate { get; set; }

    [MaxLength(1000)]
    public string? TemporaryArrangementDetails { get; set; }

    public bool RequiresEmployeeAcceptance { get; set; }
    public bool RequiresHandover { get; set; }

    public bool IsReorganization { get; set; }
    public bool IsSuccessionPlan { get; set; }
    public Guid? SuccessionPlanId { get; set; }

    public Guid? BasedOnAppraisalId { get; set; }

    [MaxLength(2000)]
    public string? AdditionalNotes { get; set; }
}

/// <summary>Submit a movement for approval workflow.</summary>
public class SubmitStaffMovementDto
{
    [Required]
    public Guid MovementId { get; set; }

    [MaxLength(500)]
    public string? SubmissionNotes { get; set; }
}

/// <summary>Final authorisation once all approval levels are cleared.</summary>
public class AuthorizeStaffMovementDto
{
    [Required]
    public Guid MovementId { get; set; }

    [MaxLength(2000)]
    public string? Comments { get; set; }
}

/// <summary>Employee accepts or declines the proposed movement.</summary>
public class RespondToStaffMovementDto
{
    [Required]
    public Guid MovementId { get; set; }

    [Required]
    public bool Accepted { get; set; }

    [MaxLength(1000)]
    public string? Comments { get; set; }
}

/// <summary>Reject a movement at any stage before authorisation.</summary>
public class RejectStaffMovementDto
{
    [Required]
    public Guid MovementId { get; set; }

    [Required]
    [MaxLength(1000)]
    public string RejectionReason { get; set; } = string.Empty;
}

/// <summary>Cancel a movement that has not yet taken effect.</summary>
public class CancelStaffMovementDto
{
    [Required]
    public Guid MovementId { get; set; }

    [Required]
    [MaxLength(1000)]
    public string CancellationReason { get; set; } = string.Empty;
}

/// <summary>Mark the handover stage as completed.</summary>
public class CompleteHandoverDto
{
    [Required]
    public Guid MovementId { get; set; }

    [MaxLength(2000)]
    public string? HandoverNotes { get; set; }
}

/// <summary>Process the return of an employee from a temporary assignment.</summary>
public class ProcessReturnFromTemporaryDto
{
    [Required]
    public Guid MovementId { get; set; }

    [Required]
    public DateTime ActualReturnDate { get; set; }

    /// <summary>
    /// The follow-on movement record that formalises the return
    /// (e.g. reinstatement to original position).
    /// </summary>
    public Guid? ReturnMovementId { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }
}

#endregion

// ============================================================================
// STAFF MOVEMENT APPROVAL LEVEL DTOs
// ============================================================================

#region Staff Movement Approval Level

public class StaffMovementApprovalLevelDto : BaseDto
{
    public Guid MovementId { get; set; }
    public string MovementNumber { get; set; } = string.Empty;
    public int Level { get; set; }
    public string RoleName { get; set; } = string.Empty;
    public Guid ApproverId { get; set; }
    public string ApproverName { get; set; } = string.Empty;
    public ApprovalStatus Status { get; set; }
    public string StatusName => Status.ToString();
    public DateTime? ActionDate { get; set; }
    public string? Comments { get; set; }
    public Guid? DelegatedToId { get; set; }
    public string? DelegatedToName { get; set; }
    public DateTime? DelegationDate { get; set; }
    public string? DelegationReason { get; set; }
}

public class CreateStaffMovementApprovalLevelDto : CreateDtoBase
{
    [Required]
    public Guid MovementId { get; set; }

    [Required]
    [Range(1, 100)]
    public int Level { get; set; }

    [Required]
    [MaxLength(100)]
    public string RoleName { get; set; } = string.Empty;

    [Required]
    public Guid ApproverId { get; set; }
}

/// <summary>Record an approval or rejection decision at a specific level.</summary>
public class ActionApprovalLevelDto
{
    [Required]
    public Guid ApprovalLevelId { get; set; }

    [Required]
    public ApprovalStatus Decision { get; set; }

    [MaxLength(1000)]
    public string? Comments { get; set; }
}

/// <summary>Delegate an approval level to another person.</summary>
public class DelegateApprovalLevelDto
{
    [Required]
    public Guid ApprovalLevelId { get; set; }

    [Required]
    public Guid DelegatedToId { get; set; }

    [MaxLength(500)]
    public string? DelegationReason { get; set; }
}

#endregion

// ============================================================================
// STAFF MOVEMENT STATUS HISTORY DTOs
// ============================================================================

#region Staff Movement Status History

/// <summary>
/// Immutable audit log entry for a status transition. Never mutated after creation.
/// </summary>
public class StaffMovementStatusHistoryDto : BaseDto
{
    public Guid MovementId { get; set; }
    public string MovementNumber { get; set; } = string.Empty;
    public StaffMovementStatus FromStatus { get; set; }
    public string FromStatusName => FromStatus.ToString();
    public StaffMovementStatus ToStatus { get; set; }
    public string ToStatusName => ToStatus.ToString();
    public DateTime ChangedDate { get; set; }
    public Guid ChangedById { get; set; }
    public string ChangedByName { get; set; } = string.Empty;
    public string? Reason { get; set; }
}

#endregion

// ============================================================================
// STAFF MOVEMENT ATTACHMENT DTOs
// ============================================================================

#region Staff Movement Attachment

public class StaffMovementAttachmentDto : BaseDto
{
    public Guid TenantId { get; set; }
    public Guid MovementId { get; set; }
    public string MovementNumber { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public StaffMovementAttachmentType Type { get; set; }
    public string TypeName => Type.ToString();
    public string? Description { get; set; }
    public DateTime UploadDate { get; set; }
    public Guid UploadedById { get; set; }
    public string UploadedByName { get; set; } = string.Empty;
}

public class CreateStaffMovementAttachmentDto : CreateDtoBase
{
    [Required]
    public Guid MovementId { get; set; }

    [Required]
    [MaxLength(200)]
    public string FileName { get; set; } = string.Empty;

    /// <summary>
    /// Legacy storage path. Set only by the migration utility for pre-existing rows — the
    /// upload endpoint leaves it empty and uses <see cref="FileUploadRecordId"/>. An API
    /// caller supplying this would be choosing which bytes on disk an attachment points at,
    /// so the controller rejects it.
    /// </summary>
    [MaxLength(500)]
    public string FilePath { get; set; } = string.Empty;

    /// <summary>Scanned controlled upload backing this attachment.</summary>
    public Guid? FileUploadRecordId { get; set; }

    /// <summary>Central-DMS record, once registered.</summary>
    public Guid? DocumentRecordId { get; set; }

    /// <summary>Central-DMS version, once registered.</summary>
    public Guid? DocumentVersionId { get; set; }

    [Required]
    public StaffMovementAttachmentType Type { get; set; }

    [MaxLength(1000)]
    public string? Description { get; set; }
}

#endregion

// ============================================================================
// STAFF MOVEMENT CHECKLIST ITEM DTOs
// ============================================================================

#region Staff Movement Checklist Item

public class StaffMovementChecklistItemDto : BaseDto
{
    public Guid TenantId { get; set; }
    public Guid MovementId { get; set; }
    public string TaskDescription { get; set; } = string.Empty;
    public StaffMovementChecklistCategory Category { get; set; }
    public string CategoryName => Category.ToString();
    public bool IsRequired { get; set; }
    public Guid? ResponsiblePersonId { get; set; }
    public string? ResponsiblePersonName { get; set; }
    public DateTime? DueDate { get; set; }
    public bool IsCompleted { get; set; }
    public DateTime? CompletionDate { get; set; }
    public string? CompletionNotes { get; set; }
    public int DisplayOrder { get; set; }
}

public class CreateStaffMovementChecklistItemDto : CreateDtoBase
{
    [Required]
    public Guid MovementId { get; set; }

    [Required]
    [MaxLength(500)]
    public string TaskDescription { get; set; } = string.Empty;

    [Required]
    public StaffMovementChecklistCategory Category { get; set; }

    public bool IsRequired { get; set; }
    public Guid? ResponsiblePersonId { get; set; }
    public DateTime? DueDate { get; set; }
    public int DisplayOrder { get; set; }
}

public class CompleteChecklistItemDto
{
    [Required]
    public Guid ItemId { get; set; }

    [MaxLength(1000)]
    public string? CompletionNotes { get; set; }
}

#endregion

// ============================================================================
// STAFF PROMOTION DTOs
// ============================================================================

#region Staff Promotion

public class StaffPromotionDto : BaseDto
{
    public Guid TenantId { get; set; }
    public Guid MovementId { get; set; }
    public string MovementNumber { get; set; } = string.Empty;
    public StaffPromotionType Type { get; set; }
    public string TypeName => Type.ToString();

    /// <summary>Number of SalaryGrade bands the employee moved up. Zero = within-grade advancement.</summary>
    public int GradeLevelIncrease { get; set; }

    public bool IsActingPromotion { get; set; }
    public DateTime? ActingPeriodEndDate { get; set; }
    public string? ActingConditions { get; set; }
    public string? AdditionalResponsibilities { get; set; }
    public bool RequiresTraining { get; set; }
    public string? RequiredTraining { get; set; }
}

public class CreateStaffPromotionDto : CreateDtoBase
{
    [Required]
    public Guid MovementId { get; set; }

    [Required]
    public StaffPromotionType Type { get; set; }

    [Range(0, int.MaxValue)]
    public int GradeLevelIncrease { get; set; }

    public bool IsActingPromotion { get; set; }
    public DateTime? ActingPeriodEndDate { get; set; }

    [MaxLength(500)]
    public string? ActingConditions { get; set; }

    [MaxLength(1000)]
    public string? AdditionalResponsibilities { get; set; }

    public bool RequiresTraining { get; set; }

    [MaxLength(1000)]
    public string? RequiredTraining { get; set; }
}

public class UpdateStaffPromotionDto : UpdateDtoBase
{
    [Required]
    public StaffPromotionType Type { get; set; }

    [Range(0, int.MaxValue)]
    public int GradeLevelIncrease { get; set; }

    public bool IsActingPromotion { get; set; }
    public DateTime? ActingPeriodEndDate { get; set; }

    [MaxLength(500)]
    public string? ActingConditions { get; set; }

    [MaxLength(1000)]
    public string? AdditionalResponsibilities { get; set; }

    public bool RequiresTraining { get; set; }

    [MaxLength(1000)]
    public string? RequiredTraining { get; set; }
}

#endregion

// ============================================================================
// STAFF TRANSFER DTOs
// ============================================================================

#region Staff Transfer

public class StaffTransferDto : BaseDto
{
    public Guid TenantId { get; set; }
    public Guid MovementId { get; set; }
    public string MovementNumber { get; set; } = string.Empty;
    public StaffTransferType Type { get; set; }
    public string TypeName => Type.ToString();
    public StaffTransferReasonCategory ReasonCategory { get; set; }
    public string ReasonCategoryName => ReasonCategory.ToString();

    // ── Relocation ────────────────────────────────────────────────────────────
    public bool RequiresRelocation { get; set; }
    public bool RelocationAssistanceProvided { get; set; }
    public decimal? RelocationAllowance { get; set; }
    public string? RelocationDetails { get; set; }

    // ── Housing ───────────────────────────────────────────────────────────────
    public bool HousingAssistanceProvided { get; set; }
    public string? HousingDetails { get; set; }

    // ── Transition Period ─────────────────────────────────────────────────────
    public int TransitionPeriodDays { get; set; }
    public DateTime? TransitionStartDate { get; set; }
    public DateTime? TransitionEndDate { get; set; }
    public Guid? ReplacementEmployeeId { get; set; }
    public string? ReplacementEmployeeName { get; set; }

    // ── Inter-Company Transfer ────────────────────────────────────────────────
    public bool IsInterCompany { get; set; }
    public Guid? DestinationCompanyId { get; set; }
    public bool EmploymentContinues { get; set; }
    public string? InterCompanyTransferDetails { get; set; }
}

public class CreateStaffTransferDto : CreateDtoBase
{
    [Required]
    public Guid MovementId { get; set; }

    [Required]
    public StaffTransferType Type { get; set; }

    [Required]
    public StaffTransferReasonCategory ReasonCategory { get; set; }

    public bool RequiresRelocation { get; set; }
    public bool RelocationAssistanceProvided { get; set; }

    [Range(0, double.MaxValue)]
    public decimal? RelocationAllowance { get; set; }

    [MaxLength(1000)]
    public string? RelocationDetails { get; set; }

    public bool HousingAssistanceProvided { get; set; }

    [MaxLength(1000)]
    public string? HousingDetails { get; set; }

    [Range(0, int.MaxValue)]
    public int TransitionPeriodDays { get; set; }

    public DateTime? TransitionStartDate { get; set; }
    public DateTime? TransitionEndDate { get; set; }
    public Guid? ReplacementEmployeeId { get; set; }

    public bool IsInterCompany { get; set; }
    public Guid? DestinationCompanyId { get; set; }
    public bool EmploymentContinues { get; set; }

    [MaxLength(1000)]
    public string? InterCompanyTransferDetails { get; set; }
}

public class UpdateStaffTransferDto : UpdateDtoBase
{
    [Required]
    public StaffTransferType Type { get; set; }

    [Required]
    public StaffTransferReasonCategory ReasonCategory { get; set; }

    public bool RequiresRelocation { get; set; }
    public bool RelocationAssistanceProvided { get; set; }

    [Range(0, double.MaxValue)]
    public decimal? RelocationAllowance { get; set; }

    [MaxLength(1000)]
    public string? RelocationDetails { get; set; }

    public bool HousingAssistanceProvided { get; set; }

    [MaxLength(1000)]
    public string? HousingDetails { get; set; }

    [Range(0, int.MaxValue)]
    public int TransitionPeriodDays { get; set; }

    public DateTime? TransitionStartDate { get; set; }
    public DateTime? TransitionEndDate { get; set; }
    public Guid? ReplacementEmployeeId { get; set; }

    public bool IsInterCompany { get; set; }
    public Guid? DestinationCompanyId { get; set; }
    public bool EmploymentContinues { get; set; }

    [MaxLength(1000)]
    public string? InterCompanyTransferDetails { get; set; }
}

#endregion

// ============================================================================
// STAFF DEMOTION DTOs
// ============================================================================

#region Staff Demotion

public class StaffDemotionDto : BaseDto
{
    public Guid TenantId { get; set; }
    public Guid MovementId { get; set; }
    public string MovementNumber { get; set; } = string.Empty;

    /// <summary>
    /// Who was demoted. Read off the movement's employee, so — like <see cref="MovementNumber"/> —
    /// it is populated by the list reads, which load the movement graph, and empty on the by-id read,
    /// which loads the row alone. A worklist of appeals needs a name on it; a movement number is not
    /// a person.
    /// </summary>
    public string EmployeeName { get; set; } = string.Empty;
    public string? EmployeeNumber { get; set; }

    public StaffDemotionReason Reason { get; set; }
    public string ReasonName => Reason.ToString();

    /// <summary>Number of SalaryGrade bands the employee moved down. Zero = within-grade reduction.</summary>
    public int GradeLevelDecrease { get; set; }

    // ── Disciplinary Link ─────────────────────────────────────────────────────
    public bool IsDisciplinaryAction { get; set; }
    public Guid? DisciplinaryActionId { get; set; }
    public string? DisciplinaryCaseNumber { get; set; }

    // ── Performance Link ──────────────────────────────────────────────────────
    public bool IsPerformanceRelated { get; set; }
    public Guid? PerformanceImprovementPlanId { get; set; }
    public string? PipNumber { get; set; }

    // ── Employee Rights ───────────────────────────────────────────────────────
    public bool EmployeeNotified { get; set; }
    public DateTime? NotificationDate { get; set; }
    public bool RightToAppeal { get; set; }
    public DateTime? AppealDeadline { get; set; }
    public string? EmployeeResponse { get; set; }
    public DateTime? EmployeeResponseDate { get; set; }
}

public class CreateStaffDemotionDto : CreateDtoBase
{
    [Required]
    public Guid MovementId { get; set; }

    [Required]
    public StaffDemotionReason Reason { get; set; }

    [Range(0, int.MaxValue)]
    public int GradeLevelDecrease { get; set; }

    public bool IsDisciplinaryAction { get; set; }
    public Guid? DisciplinaryActionId { get; set; }

    public bool IsPerformanceRelated { get; set; }
    public Guid? PerformanceImprovementPlanId { get; set; }

    public bool RightToAppeal { get; set; }
    public DateTime? AppealDeadline { get; set; }
}

public class UpdateStaffDemotionDto : UpdateDtoBase
{
    [Required]
    public StaffDemotionReason Reason { get; set; }

    [Range(0, int.MaxValue)]
    public int GradeLevelDecrease { get; set; }

    public bool EmployeeNotified { get; set; }
    public DateTime? NotificationDate { get; set; }
    public bool RightToAppeal { get; set; }
    public DateTime? AppealDeadline { get; set; }

    // EmployeeResponse and EmployeeResponseDate are NOT on this DTO. The demoted employee's own
    // words have a single writer — POST staff-demotions/{id}/respond, which refuses every caller
    // but them — and accepting them here made an ordinary HR edit a way round that.
}

#endregion

// ============================================================================
// STAFF SECONDMENT DTOs
// ============================================================================

#region Staff Secondment

public class StaffSecondmentDto : BaseDto
{
    public Guid TenantId { get; set; }
    public Guid MovementId { get; set; }
    public string MovementNumber { get; set; } = string.Empty;
    public StaffSecondmentType Type { get; set; }
    public string TypeName => Type.ToString();
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }

    /// <summary>Derived: (EndDate.Year - StartDate.Year) * 12 + EndDate.Month - StartDate.Month.</summary>
    public int DurationMonths { get; set; }

    // ── External Secondment ───────────────────────────────────────────────────
    public bool IsExternal { get; set; }
    public string? HostOrganization { get; set; }
    public string? HostOrganizationContact { get; set; }

    // ── Financial Terms ───────────────────────────────────────────────────────
    public string TermsAndConditions { get; set; } = string.Empty;
    public bool SalaryPaidByHomeOrganization { get; set; }
    public bool AllowancesPaidByHostOrganization { get; set; }
    public decimal? SecondmentAllowance { get; set; }

    // ── Objectives ────────────────────────────────────────────────────────────
    public string Objectives { get; set; } = string.Empty;
    public string? ExpectedOutcomes { get; set; }

    // ── Return Arrangements ───────────────────────────────────────────────────
    public bool ReturnGuaranteed { get; set; }
    public string? ReturnArrangements { get; set; }

    // ── Extension ────────────────────────────────────────────────────────────
    public bool ExtensionAllowed { get; set; }
    public int? MaxExtensionMonths { get; set; }
}

public class CreateStaffSecondmentDto : CreateDtoBase
{
    [Required]
    public Guid MovementId { get; set; }

    [Required]
    public StaffSecondmentType Type { get; set; }

    [Required]
    public DateTime StartDate { get; set; }

    [Required]
    public DateTime EndDate { get; set; }

    public bool IsExternal { get; set; }

    [MaxLength(200)]
    public string? HostOrganization { get; set; }

    [MaxLength(200)]
    public string? HostOrganizationContact { get; set; }

    [Required]
    [MaxLength(1000)]
    public string TermsAndConditions { get; set; } = string.Empty;

    public bool SalaryPaidByHomeOrganization { get; set; } = true;
    public bool AllowancesPaidByHostOrganization { get; set; }

    [Range(0, double.MaxValue)]
    public decimal? SecondmentAllowance { get; set; }

    [Required]
    [MaxLength(2000)]
    public string Objectives { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? ExpectedOutcomes { get; set; }

    public bool ReturnGuaranteed { get; set; } = true;

    [MaxLength(1000)]
    public string? ReturnArrangements { get; set; }

    public bool ExtensionAllowed { get; set; }

    [Range(1, 60)]
    public int? MaxExtensionMonths { get; set; }
}

public class UpdateStaffSecondmentDto : UpdateDtoBase
{
    [Required]
    public StaffSecondmentType Type { get; set; }

    [Required]
    public DateTime StartDate { get; set; }

    [Required]
    public DateTime EndDate { get; set; }

    public bool IsExternal { get; set; }

    [MaxLength(200)]
    public string? HostOrganization { get; set; }

    [MaxLength(200)]
    public string? HostOrganizationContact { get; set; }

    [Required]
    [MaxLength(1000)]
    public string TermsAndConditions { get; set; } = string.Empty;

    public bool SalaryPaidByHomeOrganization { get; set; }
    public bool AllowancesPaidByHostOrganization { get; set; }

    [Range(0, double.MaxValue)]
    public decimal? SecondmentAllowance { get; set; }

    [Required]
    [MaxLength(2000)]
    public string Objectives { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? ExpectedOutcomes { get; set; }

    public bool ReturnGuaranteed { get; set; }

    [MaxLength(1000)]
    public string? ReturnArrangements { get; set; }

    public bool ExtensionAllowed { get; set; }

    [Range(1, 60)]
    public int? MaxExtensionMonths { get; set; }
}

public class ExtendStaffSecondmentDto
{
    [Required]
    public Guid SecondmentId { get; set; }

    [Required]
    public DateTime NewEndDate { get; set; }

    [Required]
    [Range(1, 60)]
    public int ExtensionMonths { get; set; }

    [MaxLength(1000)]
    public string? ExtensionReason { get; set; }
}

#endregion

// ============================================================================
// STAFF ACTING APPOINTMENT DTOs
// ============================================================================

#region Staff Acting Appointment

public class StaffActingAppointmentDto : BaseDto
{
    public Guid TenantId { get; set; }
    public string AppointmentNumber { get; set; } = string.Empty;

    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public string? EmployeeNumber { get; set; }

    public Guid ActingPositionId { get; set; }
    public string ActingPositionTitle { get; set; } = string.Empty;

    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }

    public StaffActingReason Reason { get; set; }
    public string ReasonName => Reason.ToString();

    // ── Acting For ────────────────────────────────────────────────────────────
    public Guid? ActingForEmployeeId { get; set; }
    public string? ActingForEmployeeName { get; set; }

    // ── Compensation ──────────────────────────────────────────────────────────
    public bool ReceivesActingAllowance { get; set; }
    public decimal? ActingAllowance { get; set; }
    public HRAllowanceCalculationMethod? AllowanceCalculation { get; set; }
    public string? AllowanceCalculationName => AllowanceCalculation?.ToString();

    // ── Movement Linkage ──────────────────────────────────────────────────────
    public Guid? MovementId { get; set; }
    public string? MovementNumber { get; set; }

    // ── Status ────────────────────────────────────────────────────────────────
    public StaffActingStatus Status { get; set; }
    public string StatusName => Status.ToString();
    public DateTime? CompletionDate { get; set; }

    // ── Permanent Conversion ──────────────────────────────────────────────────
    public bool ConvertedToPermanent { get; set; }
    public DateTime? ConversionDate { get; set; }
    public Guid? ConversionMovementId { get; set; }
    public string? ConversionMovementNumber { get; set; }

    public string? Notes { get; set; }
}

public class StaffActingAppointmentSummaryDto
{
    public Guid Id { get; set; }
    public string AppointmentNumber { get; set; } = string.Empty;
    public string EmployeeName { get; set; } = string.Empty;
    public string? EmployeeNumber { get; set; }
    public string ActingPositionTitle { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public StaffActingReason Reason { get; set; }
    public string ReasonName => Reason.ToString();
    public StaffActingStatus Status { get; set; }
    public string StatusName => Status.ToString();
    public bool ConvertedToPermanent { get; set; }

    /// <summary>Optional link to a parent staff movement workflow record.</summary>
    public Guid? MovementId { get; set; }
}

public class CreateStaffActingAppointmentDto : CreateDtoBase
{
    [Required]
    public Guid EmployeeId { get; set; }

    [Required]
    public Guid ActingPositionId { get; set; }

    [Required]
    public DateTime StartDate { get; set; }

    public DateTime? EndDate { get; set; }

    [Required]
    public StaffActingReason Reason { get; set; }

    public Guid? ActingForEmployeeId { get; set; }

    public bool ReceivesActingAllowance { get; set; }

    [Range(0, double.MaxValue)]
    public decimal? ActingAllowance { get; set; }

    public HRAllowanceCalculationMethod? AllowanceCalculation { get; set; }

    /// <summary>Optional: set when this appointment originates from a formal StaffMovement workflow.</summary>
    public Guid? MovementId { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }
}

public class UpdateStaffActingAppointmentDto : UpdateDtoBase
{
    public DateTime? EndDate { get; set; }

    public bool ReceivesActingAllowance { get; set; }

    [Range(0, double.MaxValue)]
    public decimal? ActingAllowance { get; set; }

    public HRAllowanceCalculationMethod? AllowanceCalculation { get; set; }

    public StaffActingStatus Status { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }
}

public class CompleteStaffActingAppointmentDto
{
    [Required]
    public Guid AppointmentId { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }
}

/// <summary>Extends an active acting appointment to a new end date.</summary>
public class ExtendStaffActingAppointmentDto
{
    [Required]
    public Guid AppointmentId { get; set; }

    [Required]
    public DateTime NewEndDate { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }
}

/// <summary>Convert a completed acting appointment into a permanent promotion.</summary>
public class ConvertActingToPermanentDto
{
    [Required]
    public Guid AppointmentId { get; set; }

    /// <summary>
    /// The StaffMovement record that formalises the permanent promotion.
    /// Must already exist and be of type Promotion.
    /// </summary>
    [Required]
    public Guid ConversionMovementId { get; set; }
}

#endregion

// ============================================================================
// EMPLOYEE CAREER PATH DTOs
// ============================================================================

#region Employee Career Path

public class EmployeeCareerPathDto : BaseDto
{
    public Guid TenantId { get; set; }
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;

    public Guid PositionId { get; set; }
    public string PositionTitle { get; set; } = string.Empty;

    // ── Organisational Placement ──────────────────────────────────────────────
    public Guid OrganizationUnitId { get; set; }
    public string OrganizationUnitName { get; set; } = string.Empty;
    public Guid? OrganizationLevelId { get; set; }
    public string? OrganizationLevelName { get; set; }

    // ── Location ──────────────────────────────────────────────────────────────
    public Guid? LocationId { get; set; }
    public string? LocationName { get; set; }
    public Guid? LocationLevelId { get; set; }
    public string? LocationLevelName { get; set; }

    // ── Tenure ────────────────────────────────────────────────────────────────
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public bool IsCurrent { get; set; }

    /// <summary>Derived: months in this career step.</summary>
    public int DurationMonths => IsCurrent
        ? (int)((DateTime.UtcNow - StartDate).TotalDays / 30.44)
        : EndDate.HasValue ? (int)((EndDate.Value - StartDate).TotalDays / 30.44) : 0;

    // ── Causative Movement ────────────────────────────────────────────────────
    public Guid? MovementId { get; set; }
    public string? MovementNumber { get; set; }
    public StaffMovementType? MovementType { get; set; }
    public string? MovementTypeName => MovementType?.ToString();

    // ── Salary & Grade Snapshot ───────────────────────────────────────────────
    public decimal Salary { get; set; }
    public Guid? SalaryGradeId { get; set; }
    public string? SalaryGradeCode { get; set; }
    public string? SalaryGradeName { get; set; }
    public Guid? SalaryLevelId { get; set; }
    public string? SalaryLevelName { get; set; }
    public Guid? SalaryNotchId { get; set; }
    public int? SalaryNotchNumber { get; set; }

    // ── Context ───────────────────────────────────────────────────────────────
    public string? Achievements { get; set; }
    public string? KeyProjects { get; set; }
}

public class EmployeeCareerPathSummaryDto
{
    public Guid Id { get; set; }

    // The ids the timeline actually needs: without PositionId it cannot tell which post a step is,
    // and without MovementId it cannot link the movement that caused the change — which is the one
    // thing a career step is for.
    public Guid EmployeeId { get; set; }
    public Guid PositionId { get; set; }
    public Guid? MovementId { get; set; }
    public string? MovementNumber { get; set; }

    public string PositionTitle { get; set; } = string.Empty;
    public string OrganizationUnitName { get; set; } = string.Empty;
    public string? LocationName { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public bool IsCurrent { get; set; }
    public int DurationMonths { get; set; }
    public decimal Salary { get; set; }
    public string? SalaryGradeName { get; set; }
    public StaffMovementType? MovementType { get; set; }
    public string? MovementTypeName => MovementType?.ToString();
}

public class CreateEmployeeCareerPathDto : CreateDtoBase
{
    [Required]
    public Guid EmployeeId { get; set; }

    [Required]
    public Guid PositionId { get; set; }

    [Required]
    public Guid OrganizationUnitId { get; set; }

    public Guid? OrganizationLevelId { get; set; }
    public Guid? LocationId { get; set; }
    public Guid? LocationLevelId { get; set; }

    [Required]
    public DateTime StartDate { get; set; }

    public DateTime? EndDate { get; set; }
    public bool IsCurrent { get; set; }
    public Guid? MovementId { get; set; }

    [Required]
    [Range(0, double.MaxValue)]
    public decimal Salary { get; set; }

    public Guid? SalaryGradeId { get; set; }
    public Guid? SalaryLevelId { get; set; }
    public Guid? SalaryNotchId { get; set; }

    [MaxLength(2000)]
    public string? Achievements { get; set; }

    [MaxLength(2000)]
    public string? KeyProjects { get; set; }
}

public class UpdateEmployeeCareerPathDto : UpdateDtoBase
{
    public DateTime? EndDate { get; set; }
    public bool IsCurrent { get; set; }

    [MaxLength(2000)]
    public string? Achievements { get; set; }

    [MaxLength(2000)]
    public string? KeyProjects { get; set; }
}

#endregion

// ============================================================================
// MOVEMENT ANALYTICS DASHBOARD
// ============================================================================

#region Movement Dashboard

/// <summary>
/// Aggregated executive dashboard payload for staff movement activity.
/// All counters are computed server-side and returned in a single call.
/// </summary>
/// <summary>
/// Full dashboard response returned by GET /api/staff-movements/dashboard.
/// Property names are intentionally aligned with the Blazor MovementDashboardSummaryDto
/// so the JSON deserialises without any mapping layer.
/// </summary>
public class StaffMovementDashboardDto
{
    // ── KPI tiles ─────────────────────────────────────────────────────────────

    /// <summary>Movements in any non-terminal status.</summary>
    public int TotalActive { get; set; }

    /// <summary>Movements at any pending-approval stage.</summary>
    public int PendingApproval { get; set; }

    /// <summary>Movements approved (or implemented) with RequestDate in the current calendar month.</summary>
    public int ApprovedThisMonth { get; set; }

    /// <summary>Movements rejected with RequestDate in the current calendar month.</summary>
    public int RejectedThisMonth { get; set; }

    /// <summary>Movements in EmployeeAcceptancePending status.</summary>
    public int AwaitingEmployeeResponse { get; set; }

    /// <summary>Approved movements that require handover but have not completed it.</summary>
    public int AwaitingHandover { get; set; }

    /// <summary>Temporary assignments (any type) ending within 30 days.</summary>
    public int ExpiringSecondments { get; set; }

    /// <summary>ActingAppointment-type movements in a non-terminal status.</summary>
    public int ActiveActingAppointments { get; set; }

    /// <summary>Approved movements not yet implemented.</summary>
    public int AwaitingImplementation { get; set; }

    /// <summary>Pending movements whose approval has been outstanding for more than 5 business days.</summary>
    public int OverdueApprovalActions { get; set; }

    /// <summary>Movements with status = Implemented in the current calendar year.</summary>
    public int ImplementedYtd { get; set; }

    // ── Legacy scalar counters (kept for backward compatibility) ──────────────

    public int TotalMovementsYtd { get; set; }
    public decimal AverageSalaryIncreasePercentage { get; set; }
    public int MovementsWithSalaryIncrease { get; set; }
    public int? FilterYear { get; set; }
    public DateTime ComputedAt { get; set; } = DateTime.UtcNow;

    // ── Per-type breakdown ────────────────────────────────────────────────────

    /// <summary>One entry per StaffMovementType that has at least one active movement.</summary>
    public List<MovementTypeSummaryDto> ByType { get; set; } = new();

    // ── Lists ─────────────────────────────────────────────────────────────────

    /// <summary>Last 10 movements ordered by RequestDate desc.</summary>
    public List<StaffMovementSummaryDto> Recent { get; set; } = new();

    /// <summary>Movements with a pending approval stage outstanding for more than 5 days.</summary>
    public List<OverdueApprovalSummaryDto> OverdueApprovals { get; set; } = new();

    /// <summary>Temporary assignments ending within 60 days.</summary>
    public List<ExpiringAssignmentSummaryDto> ExpiringAssignments { get; set; } = new();

    /// <summary>Org units with the most pending approval actions (top 5).</summary>
    public List<ApprovalBottleneckDto> BottlenecksByUnit { get; set; } = new();
}

/// <summary>Aggregate counts for a single movement type (dashboard breakdown panel).</summary>
public class MovementTypeSummaryDto
{
    public StaffMovementType MovementType       { get; set; }
    public int               TotalActive        { get; set; }
    public int               PendingApproval    { get; set; }
    public int               CompletedThisMonth { get; set; }
    public int               ImplementedYtd     { get; set; }

    /// <summary>Percentage of overall active movements represented by this type (0–100).</summary>
    public double SharePercent { get; set; }
}

/// <summary>Single overdue-approval entry for the alert panel.</summary>
public class OverdueApprovalSummaryDto
{
    public Guid              MovementId          { get; set; }
    public string            MovementNumber      { get; set; } = string.Empty;
    public string            EmployeeName        { get; set; } = string.Empty;
    public string?           EmployeeNumber      { get; set; }
    public StaffMovementType MovementType        { get; set; }
    public StaffMovementStatus Status            { get; set; }

    /// <summary>Human-readable label of the current approval stage.</summary>
    public string PendingStage        { get; set; } = string.Empty;

    /// <summary>Name of the first pending approver (if available).</summary>
    public string PendingApproverName { get; set; } = string.Empty;

    /// <summary>Calendar days the movement has been in the current pending stage.</summary>
    public int    OverdueDays         { get; set; }

    public DateTime? EffectiveDate { get; set; }

    /// <summary>1 = Critical (>10 days), 2 = High (5–10 days), 3 = Medium (1–4 days).</summary>
    public int Priority { get; set; }
}

/// <summary>A temporary assignment (secondment or acting) nearing its end date.</summary>
public class ExpiringAssignmentSummaryDto
{
    public Guid              MovementId       { get; set; }
    public string            MovementNumber   { get; set; } = string.Empty;
    public string            EmployeeName     { get; set; } = string.Empty;
    public string?           EmployeeNumber   { get; set; }
    public StaffMovementType AssignmentType   { get; set; }
    public DateTime          EndDate          { get; set; }

    /// <summary>Calendar days until the end date (negative = already past end date).</summary>
    public int   DaysRemaining    { get; set; }

    public bool  ReturnProcessed  { get; set; }
    public string? CurrentPosition  { get; set; }
    public string? NewPosition      { get; set; }
    public string? HostOrganization { get; set; }
}

/// <summary>Workflow bottleneck summary for a single organisational unit.</summary>
public class ApprovalBottleneckDto
{
    public Guid   OrganizationUnitId   { get; set; }
    public string OrganizationUnitName { get; set; } = string.Empty;
    public int    PendingCount         { get; set; }
    public int    AverageWaitDays      { get; set; }
    public string MostDelayedStage     { get; set; } = string.Empty;
    public int    NearingEffectiveDate { get; set; }
}

/// <summary>Lightweight alert DTO kept for backward compatibility with older callers.</summary>
public class MovementPendingAlertDto
{
    public Guid MovementId { get; set; }
    public string MovementNumber { get; set; } = string.Empty;
    public string EmployeeName { get; set; } = string.Empty;
    public StaffMovementType MovementType { get; set; }
    public string MovementTypeName => MovementType.ToString();
    public DateTime EffectiveDate { get; set; }
    public StaffMovementStatus Status { get; set; }
    public string StatusName => Status.ToString();
    public string? AlertDetail { get; set; }
}

#endregion

// ============================================================================
// REMINDER ENGINE DTOs (area 8 slice 5)
// ============================================================================

#region Staff Movement Reminders

public class StaffMovementReminderRunDto
{
    public Guid Id { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string Trigger { get; set; } = string.Empty;
    public int RemindersQueued { get; set; }
}

public class StaffMovementReminderRunResultDto
{
    public Guid RunId { get; set; }
    public int RemindersQueued { get; set; }

    /// <summary>Per-kind breakdown, so a run-now shows what it actually found.</summary>
    public Dictionary<string, int> ByKind { get; set; } = new();
}

public class StaffMovementReminderLogEntryDto
{
    public Guid Id { get; set; }
    public Guid RunId { get; set; }
    public string Kind { get; set; } = string.Empty;
    public string ItemType { get; set; } = string.Empty;
    public Guid EntityId { get; set; }
    public string Reference { get; set; } = string.Empty;
    public DateTime? DueDate { get; set; }
    public int DaysRemaining { get; set; }
    public int EscalationTier { get; set; }
    public DateTime DispatchedAt { get; set; }
}

#endregion
