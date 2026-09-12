using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Entities.HR.Performance;
using ErpSystem.Core.Entities.HR.StaffDiscipline;
using ErpSystem.Core.Entities.HR.SuccessionPlanning;

using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Entities.HR.PromotionTransfer;

/// <summary>
/// Base class for all employee career movements (Promotion, Demotion, Transfer,
/// Secondment, Acting). Holds every field common across all movement types.
/// </summary>
public class StaffMovement : TenantEntity
{
    [MaxLength(50)]
    public string MovementNumber { get; set; } = string.Empty;

    // -------------------------------------------------------------------------
    // Employee
    // -------------------------------------------------------------------------
    public Guid EmployeeId { get; set; }

    [ForeignKey(nameof(EmployeeId))]
    public virtual Employee Employee { get; set; } = null!;

    // -------------------------------------------------------------------------
    // Movement Classification
    // -------------------------------------------------------------------------
    public StaffMovementType MovementType { get; set; }
    public StaffMovementCategory Category { get; set; }

    // -------------------------------------------------------------------------
    // Current Position (Before Movement)
    // -------------------------------------------------------------------------
    public Guid CurrentPositionId { get; set; }

    [ForeignKey(nameof(CurrentPositionId))]
    public virtual EmployeePosition CurrentPosition { get; set; } = null!;

    public Guid CurrentOrganizationUnitId { get; set; }

    [ForeignKey(nameof(CurrentOrganizationUnitId))]
    public virtual OrganizationUnit CurrentOrganizationUnit { get; set; } = null!;

    /// <summary>
    /// Denormalised snapshot of the org level (e.g. Division, Department, Section)
    /// so reports can filter by level without joining through OrganizationUnit.
    /// </summary>
    public Guid? CurrentOrganizationLevelId { get; set; }

    [ForeignKey(nameof(CurrentOrganizationLevelId))]
    public virtual OrganizationLevel? CurrentOrganizationLevel { get; set; }

    public Guid? CurrentLocationId { get; set; }

    [ForeignKey(nameof(CurrentLocationId))]
    public virtual Location? CurrentLocation { get; set; }

    /// <summary>
    /// Denormalised snapshot of the location level (e.g. Country, Region, Office)
    /// for efficient filtering without joining through Location.
    /// </summary>
    public Guid? CurrentLocationLevelId { get; set; }

    [ForeignKey(nameof(CurrentLocationLevelId))]
    public virtual LocationLevel? CurrentLocationLevel { get; set; }

    public Guid? CurrentSupervisorId { get; set; }

    [ForeignKey(nameof(CurrentSupervisorId))]
    public virtual Employee? CurrentSupervisor { get; set; }

    // -------------------------------------------------------------------------
    // Current Salary & Grade (Before Movement)
    // SalaryGrade → SalaryLevel → SalaryNotch defines the exact pay point.
    // CurrentSalary is stored as a snapshot so the record remains accurate if
    // the pay scale is revised after the movement is processed.
    // -------------------------------------------------------------------------
    [Column(TypeName = "decimal(18,2)")]
    public decimal CurrentSalary { get; set; }

    public Guid? CurrentSalaryGradeId { get; set; }

    [ForeignKey(nameof(CurrentSalaryGradeId))]
    public virtual SalaryGrade? CurrentSalaryGrade { get; set; }

    public Guid? CurrentSalaryLevelId { get; set; }

    [ForeignKey(nameof(CurrentSalaryLevelId))]
    public virtual SalaryLevel? CurrentSalaryLevel { get; set; }

    public Guid? CurrentSalaryNotchId { get; set; }

    [ForeignKey(nameof(CurrentSalaryNotchId))]
    public virtual SalaryNotch? CurrentSalaryNotch { get; set; }

    // -------------------------------------------------------------------------
    // New Position (After Movement)
    // -------------------------------------------------------------------------
    public Guid NewPositionId { get; set; }

    [ForeignKey(nameof(NewPositionId))]
    public virtual EmployeePosition NewPosition { get; set; } = null!;

    public Guid NewOrganizationUnitId { get; set; }

    [ForeignKey(nameof(NewOrganizationUnitId))]
    public virtual OrganizationUnit NewOrganizationUnit { get; set; } = null!;

    /// <summary>
    /// Denormalised snapshot of the org level the employee is moving into.
    /// </summary>
    public Guid? NewOrganizationLevelId { get; set; }

    [ForeignKey(nameof(NewOrganizationLevelId))]
    public virtual OrganizationLevel? NewOrganizationLevel { get; set; }

    public Guid? NewLocationId { get; set; }

    [ForeignKey(nameof(NewLocationId))]
    public virtual Location? NewLocation { get; set; }

    /// <summary>
    /// Denormalised snapshot of the location level the employee is moving into.
    /// </summary>
    public Guid? NewLocationLevelId { get; set; }

    [ForeignKey(nameof(NewLocationLevelId))]
    public virtual LocationLevel? NewLocationLevel { get; set; }

    public Guid? NewSupervisorId { get; set; }

    [ForeignKey(nameof(NewSupervisorId))]
    public virtual Employee? NewSupervisor { get; set; }

    // -------------------------------------------------------------------------
    // New Salary & Grade (After Movement)
    // Service layer must set NewSalary = NewSalaryNotch.SalaryAmount (or the
    // midpoint of NewSalaryLevel when no specific notch is assigned), then
    // derive SalaryIncreaseAmount and SalaryIncreasePercentage from the delta.
    // -------------------------------------------------------------------------
    [Column(TypeName = "decimal(18,2)")]
    public decimal NewSalary { get; set; }

    public Guid? NewSalaryGradeId { get; set; }

    [ForeignKey(nameof(NewSalaryGradeId))]
    public virtual SalaryGrade? NewSalaryGrade { get; set; }

    public Guid? NewSalaryLevelId { get; set; }

    [ForeignKey(nameof(NewSalaryLevelId))]
    public virtual SalaryLevel? NewSalaryLevel { get; set; }

    public Guid? NewSalaryNotchId { get; set; }

    [ForeignKey(nameof(NewSalaryNotchId))]
    public virtual SalaryNotch? NewSalaryNotch { get; set; }

    /// <remarks>
    /// Snapshot: NewSalary - CurrentSalary. Kept explicit so reports and
    /// payroll integrations can read it without recomputing.
    /// </remarks>
    [Column(TypeName = "decimal(18,2)")]
    public decimal? SalaryIncreaseAmount { get; set; }

    /// <remarks>
    /// Snapshot: (NewSalary - CurrentSalary) / CurrentSalary * 100.
    /// Enforce consistency via the service layer when persisting.
    /// </remarks>
    [Column(TypeName = "decimal(5,2)")]
    public decimal? SalaryIncreasePercentage { get; set; }

    // -------------------------------------------------------------------------
    // Reason & Justification
    // -------------------------------------------------------------------------
    [MaxLength(500)]
    public string Reason { get; set; } = string.Empty;
    
    [MaxLength(500)]
    public string? Justification { get; set; }
    
    public bool IsReorganization { get; set; }
    public bool IsSuccessionPlan { get; set; }

    public Guid? SuccessionPlanId { get; set; }

    [ForeignKey(nameof(SuccessionPlanId))]
    public virtual SuccessionPlan? SuccessionPlan { get; set; }

    // -------------------------------------------------------------------------
    // Dates
    // -------------------------------------------------------------------------
    public DateTime RequestDate { get; set; }
    public DateTime EffectiveDate { get; set; }

    // -------------------------------------------------------------------------
    // Temporary Movement Fields (Secondment, Acting)
    // -------------------------------------------------------------------------
    public bool IsTemporary { get; set; }
    public DateTime? TemporaryEndDate { get; set; }

    [MaxLength(1000)]
    public string? TemporaryArrangementDetails { get; set; }
	
	/// <summary>
    /// Set to true once the return-from-temporary process has been completed.
    /// </summary>
    public bool ReturnProcessed { get; set; }
 
    /// <summary>
    /// The actual date the employee returned, which may differ from TemporaryEndDate.
    /// </summary>
    public DateTime? ActualReturnDate { get; set; }
 
    /// <summary>
    /// Links to the follow-on movement record that formalises the employee's return.
    /// </summary>
    public Guid? ReturnMovementId { get; set; }
 
    [ForeignKey(nameof(ReturnMovementId))]
    public virtual StaffMovement? ReturnMovement { get; set; }
	
	// -------------------------------------------------------------------------
    // Requester
    // -------------------------------------------------------------------------

    public Guid RequestedById { get; set; }

    [ForeignKey(nameof(RequestedById))]
    public virtual Employee RequestedBy { get; set; } = null!;

    public DateTime RequestSubmissionDate { get; set; }
	
    // -------------------------------------------------------------------------
    // Final Authorization
    // Nullable — populated only when all approval levels are cleared.
    // -------------------------------------------------------------------------

    public Guid? AuthorizedById { get; set; }
 
    [ForeignKey(nameof(AuthorizedById))]
    public virtual Employee? AuthorizedBy { get; set; }
 
    public DateTime? AuthorizationDate { get; set; }
    
    // -------------------------------------------------------------------------
    // Status
    // -------------------------------------------------------------------------
    public StaffMovementStatus Status { get; set; }
	
    // -------------------------------------------------------------------------
    // Rejection
    // -------------------------------------------------------------------------
    [MaxLength(1000)]
    public string? RejectionReason { get; set; }
 
    public Guid? RejectedById { get; set; }
 
    [ForeignKey(nameof(RejectedById))]
    public virtual Employee? RejectedBy { get; set; }
 
    public DateTime? RejectionDate { get; set; }
	
    // -------------------------------------------------------------------------
    // Cancellation
    // -------------------------------------------------------------------------
    [MaxLength(1000)]
    public string? CancellationReason { get; set; }
 
    public Guid? CancelledById { get; set; }
 
    [ForeignKey(nameof(CancelledById))]
    public virtual Employee? CancelledBy { get; set; }
 
    public DateTime? CancellationDate { get; set; }

    // -------------------------------------------------------------------------
    // Employee Acceptance
    // -------------------------------------------------------------------------
    public bool RequiresEmployeeAcceptance { get; set; }
    public bool? EmployeeAccepted { get; set; }
    public DateTime? EmployeeResponseDate { get; set; }

    [MaxLength(1000)]
    public string? EmployeeComments { get; set; }

    // -------------------------------------------------------------------------
    // Handover
    // -------------------------------------------------------------------------
    public bool RequiresHandover { get; set; }
    public DateTime? HandoverCompletionDate { get; set; }
 
    [MaxLength(2000)]
    public string? HandoverNotes { get; set; }

    // -------------------------------------------------------------------------
    // Performance Basis
    // -------------------------------------------------------------------------
    public Guid? BasedOnAppraisalId { get; set; }
 
    [ForeignKey(nameof(BasedOnAppraisalId))]
    public virtual PerformanceAppraisal? BasedOnAppraisal { get; set; }

    // -------------------------------------------------------------------------
    // Miscellaneous
    // -------------------------------------------------------------------------
    [MaxLength(2000)]
    public string? AdditionalNotes { get; set; }

    // -------------------------------------------------------------------------
    // Navigation Collections
    // -------------------------------------------------------------------------
    public virtual ICollection<StaffMovementApprovalLevel> ApprovalLevels { get; set; } = new List<StaffMovementApprovalLevel>();
    public virtual ICollection<StaffMovementStatusHistory> StatusHistory { get; set; } = new List<StaffMovementStatusHistory>();
    public virtual ICollection<StaffMovementAttachment> Attachments { get; set; } = new List<StaffMovementAttachment>();
    public virtual ICollection<StaffMovementChecklistItem> ChecklistItems { get; set; } = new List<StaffMovementChecklistItem>();

    // ── One-to-one subtype details ────────────────────────────────────────────
    public virtual StaffPromotion? Promotion { get; set; }
    public virtual StaffTransfer? Transfer { get; set; }
    public virtual StaffDemotion? Demotion { get; set; }
    public virtual StaffSecondment? Secondment { get; set; }
}

// =============================================================================
// APPROVAL WORKFLOW
// =============================================================================
 
/// <summary>
/// Represents one level in a configurable multi-level approval chain for a movement.
/// Example: Level 1 = Line Manager, Level 2 = HR Manager, Level 3 = Director.
/// </summary>
public class StaffMovementApprovalLevel : TenantEntity
{
    public Guid MovementId { get; set; }
 
    [ForeignKey(nameof(MovementId))]
    public virtual StaffMovement Movement { get; set; } = null!;
 
    /// <summary>Execution order — lower number is actioned first.</summary>
    public int Level { get; set; }
 
    [MaxLength(100)]
    public string RoleName { get; set; } = string.Empty; // e.g. "Line Manager", "HR Director"
 
    public Guid ApproverId { get; set; }
 
    [ForeignKey(nameof(ApproverId))]
    public virtual Employee Approver { get; set; } = null!;
 
    public ApprovalStatus Status { get; set; }
 
    public DateTime? ActionDate { get; set; }
 
    [MaxLength(1000)]
    public string? Comments { get; set; }
 
    /// <summary>
    /// When the approver delegates their action to another person.
    /// </summary>
    public Guid? DelegatedToId { get; set; }
 
    [ForeignKey(nameof(DelegatedToId))]
    public virtual Employee? DelegatedTo { get; set; }
 
    public DateTime? DelegationDate { get; set; }
 
    [MaxLength(500)]
    public string? DelegationReason { get; set; }
}

// =============================================================================
// STATUS AUDIT LOG
// =============================================================================
 
/// <summary>
/// Immutable log of every status transition on a movement. Required for HR
/// compliance, audit, and dispute resolution. Never updated — only inserted.
/// </summary>
public class StaffMovementStatusHistory : TenantEntity
{
    public Guid MovementId { get; set; }
 
    [ForeignKey(nameof(MovementId))]
    public virtual StaffMovement Movement { get; set; } = null!;
 
    public StaffMovementStatus FromStatus { get; set; }
    public StaffMovementStatus ToStatus { get; set; }
 
    public DateTime ChangedDate { get; set; }
 
    public Guid ChangedById { get; set; }
 
    [ForeignKey(nameof(ChangedById))]
    public virtual Employee ChangedBy { get; set; } = null!;
 
    [MaxLength(1000)]
    public string? Reason { get; set; }
}

// =============================================================================
// ATTACHMENTS
// =============================================================================
 
/// <summary>
/// Supporting documents attached to a movement record.
/// </summary>
public class StaffMovementAttachment : TenantEntity
{
    public Guid MovementId { get; set; }
 
    [ForeignKey(nameof(MovementId))]
    public virtual StaffMovement Movement { get; set; } = null!;
 
    [MaxLength(200)]
    public string FileName { get; set; } = string.Empty;
 
    [MaxLength(500)]
    public string FilePath { get; set; } = string.Empty;
 
    public StaffMovementAttachmentType Type { get; set; }

    /// <summary>Scanned controlled upload backing this attachment.</summary>
    public Guid? FileUploadRecordId { get; set; }

    /// <summary>Central-DMS record, once registered.</summary>
    public Guid? DocumentRecordId { get; set; }

    /// <summary>Central-DMS version, once registered.</summary>
    public Guid? DocumentVersionId { get; set; }

    [MaxLength(1000)]
    public string? Description { get; set; }
 
    public DateTime UploadDate { get; set; }
 
    public Guid UploadedById { get; set; }
 
    [ForeignKey(nameof(UploadedById))]
    public virtual Employee UploadedBy { get; set; } = null!;
}

// =============================================================================
// CHECKLIST
// =============================================================================
 
/// <summary>
/// Task checklist item associated with a movement (IT access revocation,
/// payroll updates, asset returns, etc.).
/// </summary>
public class StaffMovementChecklistItem : TenantEntity
{
    public Guid MovementId { get; set; }
 
    [ForeignKey(nameof(MovementId))]
    public virtual StaffMovement Movement { get; set; } = null!;
 
    [MaxLength(500)]
    public string TaskDescription { get; set; } = string.Empty;
 
    public StaffMovementChecklistCategory Category { get; set; }
 
    public bool IsRequired { get; set; }
 
    public Guid? ResponsiblePersonId { get; set; }
 
    [ForeignKey(nameof(ResponsiblePersonId))]
    public virtual Employee? ResponsiblePerson { get; set; }
 
    public DateTime? DueDate { get; set; }
 
    public bool IsCompleted { get; set; }
    public DateTime? CompletionDate { get; set; }
 
    [MaxLength(1000)]
    public string? CompletionNotes { get; set; }
 
    public int DisplayOrder { get; set; }
}

// =============================================================================
// MOVEMENT SUBTYPES
// =============================================================================

/// <summary>
/// Promotion-specific detail record.
/// One-to-one with StaffMovement where MovementType == Promotion.
/// </summary>
public class StaffPromotion : TenantEntity
{
    public Guid MovementId { get; set; }
 
    [ForeignKey(nameof(MovementId))]
    public virtual StaffMovement Movement { get; set; } = null!;
 
    public StaffPromotionType Type { get; set; }
 
    /// <remarks>
    /// Integer count of SalaryGrade bands the employee moved up (e.g. 2 = jumped
    /// two full grades). Computed by the service layer from
    /// StaffMovement.CurrentSalaryGrade → NewSalaryGrade and stored as a
    /// snapshot for quick reporting. Zero when the promotion is within the
    /// same grade (level or notch advancement only).
    /// </remarks>
    public int GradeLevelIncrease { get; set; }
 
    public bool IsActingPromotion { get; set; }
    public DateTime? ActingPeriodEndDate { get; set; }
 
    [MaxLength(500)]
    public string? ActingConditions { get; set; }
 
    [MaxLength(1000)]
    public string? AdditionalResponsibilities { get; set; }
 
    // Training Requirements
    public bool RequiresTraining { get; set; }
 
    [MaxLength(1000)]
    public string? RequiredTraining { get; set; }
}

/// <summary>
/// Transfer-specific detail record.
/// One-to-one with StaffMovement where MovementType == Transfer.
/// </summary>
public class StaffTransfer : TenantEntity
{
    public Guid MovementId { get; set; }
 
    [ForeignKey(nameof(MovementId))]
    public virtual StaffMovement Movement { get; set; } = null!;
 
    public StaffTransferType Type { get; set; }
 
    /// <summary>
    /// Renamed from Reason to avoid ambiguity with StaffMovement.Reason.
    /// </summary>
    public StaffTransferReasonCategory ReasonCategory { get; set; }
 
    // -------------------------------------------------------------------------
    // Relocation
    // -------------------------------------------------------------------------
    public bool RequiresRelocation { get; set; }
    public bool RelocationAssistanceProvided { get; set; }
 
    [Column(TypeName = "decimal(18,2)")]
    public decimal? RelocationAllowance { get; set; }
 
    [MaxLength(1000)]
    public string? RelocationDetails { get; set; }
 
    // -------------------------------------------------------------------------
    // Housing
    // -------------------------------------------------------------------------
    public bool HousingAssistanceProvided { get; set; }
 
    [MaxLength(1000)]
    public string? HousingDetails { get; set; }
 
    // -------------------------------------------------------------------------
    // Transition Period
    // -------------------------------------------------------------------------
    public int TransitionPeriodDays { get; set; }
    public DateTime? TransitionStartDate { get; set; }
    public DateTime? TransitionEndDate { get; set; }
 
    public Guid? ReplacementEmployeeId { get; set; }
 
    [ForeignKey(nameof(ReplacementEmployeeId))]
    public virtual Employee? ReplacementEmployee { get; set; }
 
    // -------------------------------------------------------------------------
    // Inter-Company Transfer
    // For group/conglomerate organisations where the employee moves between
    // legal entities (subsidiaries, sister companies, etc.).
    // -------------------------------------------------------------------------
    public bool IsInterCompany { get; set; }
 
    /// <summary>
    /// FK to your Company / Tenant entity representing the receiving legal entity.
    /// </summary>
    public Guid? DestinationCompanyId { get; set; }
 
    /// <summary>
    /// True  = employee keeps existing contract (continuous employment).
    /// False = employment with source company ends; new contract issued by destination.
    /// </summary>
    public bool EmploymentContinues { get; set; }
 
    [MaxLength(1000)]
    public string? InterCompanyTransferDetails { get; set; }
}

/// <summary>
/// Demotion-specific detail record.
/// One-to-one with StaffMovement where StaffMovementType == Demotion.
/// </summary>
public class StaffDemotion : TenantEntity
{
    public Guid MovementId { get; set; }
 
    [ForeignKey(nameof(MovementId))]
    public virtual StaffMovement Movement { get; set; } = null!;
 
    public StaffDemotionReason Reason { get; set; }
 
    /// <remarks>
    /// Integer count of SalaryGrade bands the employee moved down. Computed
    /// by the service layer from StaffMovement.CurrentSalaryGrade →
    /// NewSalaryGrade and stored as a snapshot for reporting. Zero when the
    /// demotion is within the same grade (level or notch reduction only).
    /// </remarks>
    public int GradeLevelDecrease { get; set; }
 
    // -------------------------------------------------------------------------
    // Disciplinary Link
    // -------------------------------------------------------------------------
    public bool IsDisciplinaryAction { get; set; }
 
    public Guid? DisciplinaryActionId { get; set; }
 
    [ForeignKey(nameof(DisciplinaryActionId))]
    public virtual StaffDisciplinaryAction? DisciplinaryAction { get; set; }
 
    // -------------------------------------------------------------------------
    // Performance Link
    // -------------------------------------------------------------------------
    public bool IsPerformanceRelated { get; set; }
 
    public Guid? PerformanceImprovementPlanId { get; set; }
 
    [ForeignKey(nameof(PerformanceImprovementPlanId))]
    public virtual PerformanceImprovementPlan? PerformanceImprovementPlan { get; set; }
 
    // -------------------------------------------------------------------------
    // Employee Rights & Notification
    // -------------------------------------------------------------------------
    public bool EmployeeNotified { get; set; }
    public DateTime? NotificationDate { get; set; }
 
    public bool RightToAppeal { get; set; }
    public DateTime? AppealDeadline { get; set; }
 
    /// <remarks>Nullable — null until the employee actually responds.</remarks>
    [MaxLength(1000)]
    public string? EmployeeResponse { get; set; }
 
    public DateTime? EmployeeResponseDate { get; set; }
}

/// <summary>
/// Secondment-specific detail record (temporary assignment to another
/// department or external organisation).
/// One-to-one with StaffMovement where StaffMovementType == Secondment.
/// </summary>
public class StaffSecondment : TenantEntity
{
    public Guid MovementId { get; set; }
 
    [ForeignKey(nameof(MovementId))]
    public virtual StaffMovement Movement { get; set; } = null!;
 
    public StaffSecondmentType Type { get; set; }
 
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
 
    /// <summary>
    /// Derived — do not store. Use this for display / reporting only.
    /// </summary>
    [NotMapped]
    public int DurationMonths => ((EndDate.Year - StartDate.Year) * 12) + EndDate.Month - StartDate.Month;
 
    // -------------------------------------------------------------------------
    // External Secondment
    // -------------------------------------------------------------------------
    public bool IsExternal { get; set; }
 
    [MaxLength(200)]
    public string? HostOrganization { get; set; }
 
    [MaxLength(200)]
    public string? HostOrganizationContact { get; set; }
 
    // -------------------------------------------------------------------------
    // Financial Terms
    // -------------------------------------------------------------------------
    [MaxLength(1000)]
    public string TermsAndConditions { get; set; } = string.Empty;
 
    public bool SalaryPaidByHomeOrganization { get; set; }
    public bool AllowancesPaidByHostOrganization { get; set; }
 
    [Column(TypeName = "decimal(18,2)")]
    public decimal? SecondmentAllowance { get; set; }
 
    // -------------------------------------------------------------------------
    // Objectives
    // -------------------------------------------------------------------------
    [MaxLength(2000)]
    public string Objectives { get; set; } = string.Empty;
 
    [MaxLength(2000)]
    public string? ExpectedOutcomes { get; set; }
 
    // -------------------------------------------------------------------------
    // Return Arrangements
    // -------------------------------------------------------------------------
    public bool ReturnGuaranteed { get; set; }
 
    [MaxLength(1000)]
    public string? ReturnArrangements { get; set; }
 
    // -------------------------------------------------------------------------
    // Extension
    // -------------------------------------------------------------------------
    public bool ExtensionAllowed { get; set; }
    public int? MaxExtensionMonths { get; set; }
}

// =============================================================================
// ACTING APPOINTMENT
// =============================================================================
 
/// <summary>
/// Standalone acting appointment (temporary elevation to a higher position).
/// May or may not originate from a formal StaffMovement record.
/// </summary>
public class StaffActingAppointment : TenantEntity
{
    [MaxLength(50)]
    public string AppointmentNumber { get; set; } = string.Empty;
 
    public Guid EmployeeId { get; set; }
 
    [ForeignKey(nameof(EmployeeId))]
    public virtual Employee Employee { get; set; } = null!;
 
    public Guid ActingPositionId { get; set; }
 
    [ForeignKey(nameof(ActingPositionId))]
    public virtual EmployeePosition ActingPosition { get; set; } = null!;
 
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
 
    public StaffActingReason Reason { get; set; }
 
    // -------------------------------------------------------------------------
    // Acting For
    // -------------------------------------------------------------------------
    public Guid? ActingForEmployeeId { get; set; }
 
    [ForeignKey(nameof(ActingForEmployeeId))]
    public virtual Employee? ActingForEmployee { get; set; }
 
    // -------------------------------------------------------------------------
    // Compensation
    // -------------------------------------------------------------------------
    public bool ReceivesActingAllowance { get; set; }
 
    [Column(TypeName = "decimal(18,2)")]
    public decimal? ActingAllowance { get; set; }
 
    public HRAllowanceCalculationMethod? AllowanceCalculation { get; set; }
 
    // -------------------------------------------------------------------------
    // Movement Linkage
    // Nullable: set when this appointment was raised via the StaffMovement
    // workflow so it is visible in career history and movement reports.
    // -------------------------------------------------------------------------
    public Guid? MovementId { get; set; }
 
    [ForeignKey(nameof(MovementId))]
    public virtual StaffMovement? Movement { get; set; }
 
    // -------------------------------------------------------------------------
    // Status
    // -------------------------------------------------------------------------
    public StaffActingStatus Status { get; set; }
    public DateTime? CompletionDate { get; set; }
 
    // -------------------------------------------------------------------------
    // Permanent Conversion
    // -------------------------------------------------------------------------
    public bool ConvertedToPermanent { get; set; }
    public DateTime? ConversionDate { get; set; }
 
    /// <summary>
    /// The formal movement record that converted this acting appointment
    /// into a permanent promotion.
    /// </summary>
    public Guid? ConversionMovementId { get; set; }
 
    [ForeignKey(nameof(ConversionMovementId))]
    public virtual StaffMovement? ConversionMovement { get; set; }
 
    [MaxLength(1000)]
    public string? Notes { get; set; }
}

// =============================================================================
// CAREER PATH HISTORY
// =============================================================================
 
/// <summary>
/// Running log of every position an employee has held. One record per
/// position, linked to the movement that caused the change where applicable.
/// </summary>
public class EmployeeCareerPath : TenantEntity
{
    public Guid EmployeeId { get; set; }
 
    [ForeignKey(nameof(EmployeeId))]
    public virtual Employee Employee { get; set; } = null!;
 
    public Guid PositionId { get; set; }
 
    [ForeignKey(nameof(PositionId))]
    public virtual EmployeePosition Position { get; set; } = null!;
 
    public Guid OrganizationUnitId { get; set; }

    [ForeignKey(nameof(OrganizationUnitId))]
    public virtual OrganizationUnit OrganizationUnit { get; set; } = null!;

    /// <summary>
    /// Denormalised snapshot of the org level at the time of this career step.
    /// </summary>
    public Guid? OrganizationLevelId { get; set; }

    [ForeignKey(nameof(OrganizationLevelId))]
    public virtual OrganizationLevel? OrganizationLevel { get; set; }

    public Guid? LocationId { get; set; }

    [ForeignKey(nameof(LocationId))]
    public virtual Location? Location { get; set; }

    /// <summary>
    /// Denormalised snapshot of the location level at the time of this career step.
    /// </summary>
    public Guid? LocationLevelId { get; set; }

    [ForeignKey(nameof(LocationLevelId))]
    public virtual LocationLevel? LocationLevel { get; set; }

    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
 
    public bool IsCurrent { get; set; }
 
    /// <summary>
    /// The movement record that caused this career step (null for the
    /// initial/hire record).
    /// </summary>
    public Guid? MovementId { get; set; }
 
    [ForeignKey(nameof(MovementId))]
    public virtual StaffMovement? Movement { get; set; }
 
    // -------------------------------------------------------------------------
    // Salary & Grade Snapshot at point of this career step
    // Salary is stored as an explicit amount so the career history remains
    // accurate if the pay scale is restructured in future. The Grade/Level/
    // Notch FKs point to the pay-scale entries that were in effect at the time.
    // -------------------------------------------------------------------------
    [Column(TypeName = "decimal(18,2)")]
    public decimal Salary { get; set; }

    public Guid? SalaryGradeId { get; set; }

    [ForeignKey(nameof(SalaryGradeId))]
    public virtual SalaryGrade? SalaryGrade { get; set; }

    public Guid? SalaryLevelId { get; set; }

    [ForeignKey(nameof(SalaryLevelId))]
    public virtual SalaryLevel? SalaryLevel { get; set; }

    public Guid? SalaryNotchId { get; set; }

    [ForeignKey(nameof(SalaryNotchId))]
    public virtual SalaryNotch? SalaryNotch { get; set; }
 
    // -------------------------------------------------------------------------
    // Achievements / Context
    // -------------------------------------------------------------------------
    [MaxLength(2000)]
    public string? Achievements { get; set; }
 
    [MaxLength(2000)]
    public string? KeyProjects { get; set; }
}

// =============================================================================
// REMINDER ENGINE (area 8 slice 5)
// =============================================================================

/// <summary>
/// One execution of the staff-movement reminder sweep, scheduled or run by hand.
/// </summary>
public class StaffMovementReminderRun : TenantEntity
{
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    /// <summary>"Scheduled" (background service) or "Manual" (run-now endpoint).</summary>
    [MaxLength(20)]
    public string Trigger { get; set; } = "Scheduled";

    public Guid? TriggeredByUserId { get; set; }

    public int RemindersQueued { get; set; }

    public virtual ICollection<StaffMovementReminderDispatchLog> DispatchLogs { get; set; }
        = new List<StaffMovementReminderDispatchLog>();
}

/// <summary>
/// One reminder actually dispatched by a sweep.
///
/// The unique (TenantId, DedupeKey) index is the send-once guarantee: a key encodes the item, the
/// reminder kind, the due date and the ladder rung (or escalation tier) reached, so each rung fires
/// exactly once — and moving a due date re-arms the ladder, because it produces new keys.
/// </summary>
public class StaffMovementReminderDispatchLog : TenantEntity
{
    public Guid RunId { get; set; }

    [ForeignKey(nameof(RunId))]
    public virtual StaffMovementReminderRun Run { get; set; } = null!;

    /// <summary>Machine kind, e.g. "TemporaryReturnOverdue", "AwaitingImplementation".</summary>
    [MaxLength(60)]
    public string Kind { get; set; } = string.Empty;

    /// <summary>Human label for the swept item, e.g. "Secondment return".</summary>
    [MaxLength(100)]
    public string ItemType { get; set; } = string.Empty;

    /// <summary>Id of the swept record. No FK — the target table varies by kind.</summary>
    public Guid EntityId { get; set; }

    /// <summary>What the notification shows: the movement or appointment number.</summary>
    [MaxLength(250)]
    public string Reference { get; set; } = string.Empty;

    public DateTime? DueDate { get; set; }

    /// <summary>Days remaining at dispatch time; negative when overdue.</summary>
    public int DaysRemaining { get; set; }

    /// <summary>0 for a due-soon rung; 1, 2 or 3 for an overdue escalation tier.</summary>
    public int EscalationTier { get; set; }

    [MaxLength(200)]
    public string DedupeKey { get; set; } = string.Empty;
}
