using System.ComponentModel.DataAnnotations;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.DTOs.HR;

// ============================================================================
// STAFF OFFENSE DTOs  (lookup master data)
// ============================================================================

#region Staff Offense

public class StaffOffenseDto : BaseDto
{
    public Guid TenantId { get; set; }
    public string OffenseCode { get; set; } = string.Empty;
    public string OffenseName { get; set; } = string.Empty;
    public string OffenseDescription { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public List<StaffOffenseProcedureDto> Procedures { get; set; } = new();
}

public class StaffOffenseSummaryDto
{
    public Guid Id { get; set; }
    public string OffenseCode { get; set; } = string.Empty;
    public string OffenseName { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public int ProcedureCount { get; set; }
}

public class CreateStaffOffenseDto : CreateDtoBase
{
    [MaxLength(50)]
    public string OffenseCode { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string OffenseName { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string OffenseDescription { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;
}

public class UpdateStaffOffenseDto : UpdateDtoBase
{
    [MaxLength(50)]
    public string OffenseCode { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string OffenseName { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string OffenseDescription { get; set; } = string.Empty;

    public bool IsActive { get; set; }
}

#endregion

// ============================================================================
// STAFF OFFENSE PROCEDURE DTOs
// ============================================================================

#region Staff Offense Procedure

public class StaffOffenseProcedureDto : BaseDto
{
    public Guid OffenseId { get; set; }
    public string OffenseName { get; set; } = string.Empty;
    public string StepName { get; set; } = string.Empty;
    public string StepDescription { get; set; } = string.Empty;
    public int Sequence { get; set; }
    public int? ExpectedCompletionDays { get; set; }
}

public class CreateStaffOffenseProcedureDto : CreateDtoBase
{
    [Required]
    public Guid OffenseId { get; set; }

    [Required]
    [MaxLength(200)]
    public string StepName { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string StepDescription { get; set; } = string.Empty;

    [Required]
    [Range(1, 999)]
    public int Sequence { get; set; }

    [Range(1, 365)]
    public int? ExpectedCompletionDays { get; set; }
}

public class UpdateStaffOffenseProcedureDto : UpdateDtoBase
{
    [Required]
    [MaxLength(200)]
    public string StepName { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string StepDescription { get; set; } = string.Empty;

    [Required]
    [Range(1, 999)]
    public int Sequence { get; set; }

    [Range(1, 365)]
    public int? ExpectedCompletionDays { get; set; }
}

/// <summary>Ordered procedure IDs (index 0 = sequence 1).</summary>
public class ReorderStaffOffenseProceduresDto
{
    [Required]
    public Guid OffenseId { get; set; }

    [Required]
    [MinLength(1)]
    public List<Guid> OrderedProcedureIds { get; set; } = new();
}

#endregion

// ============================================================================
// STAFF DISCIPLINARY ACTION TYPE DTOs  (lookup master data)
// ============================================================================

#region Staff Disciplinary Action Type

public class StaffDisciplinaryActionTypeDto : BaseDto
{
    public Guid TenantId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public int? DefaultSuspensionDays { get; set; }
    public decimal? DefaultFineAmount { get; set; }

    /// <summary>Who may issue this action — FR-HR-080 / FR-HR-092.</summary>
    public DisciplinaryActionAuthority MinimumAuthority { get; set; } = DisciplinaryActionAuthority.Hr;
    public string MinimumAuthorityName => MinimumAuthority.ToString();
}

public class StaffDisciplinaryActionTypeSummaryDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; }

    /// <summary>
    /// Carried on the summary as well as the detail: the decision dialog picks from summaries, and
    /// it needs to know which sanctions the caller is entitled to offer.
    /// </summary>
    public DisciplinaryActionAuthority MinimumAuthority { get; set; }
    public string MinimumAuthorityName => MinimumAuthority.ToString();
}

public class CreateStaffDisciplinaryActionTypeDto : CreateDtoBase
{
    [MaxLength(50)]
    public string Code { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string Description { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;
    public int? DefaultSuspensionDays { get; set; }

    [Range(0, double.MaxValue)]
    public decimal? DefaultFineAmount { get; set; }

    /// <summary>Who may issue this action. Defaults to HR — restrictive rather than permissive.</summary>
    public DisciplinaryActionAuthority MinimumAuthority { get; set; } = DisciplinaryActionAuthority.Hr;
}

public class UpdateStaffDisciplinaryActionTypeDto : UpdateDtoBase
{
    [MaxLength(50)]
    public string Code { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string Description { get; set; } = string.Empty;

    public bool IsActive { get; set; }
    public int? DefaultSuspensionDays { get; set; }

    [Range(0, double.MaxValue)]
    public decimal? DefaultFineAmount { get; set; }

    /// <summary>Who may issue this action — FR-HR-080 / FR-HR-092.</summary>
    public DisciplinaryActionAuthority MinimumAuthority { get; set; } = DisciplinaryActionAuthority.Hr;
}

#endregion

// ============================================================================
// STAFF DISCIPLINARY ACTION DTOs  (case header)
// ============================================================================

#region Staff Disciplinary Action

/// <summary>
/// Full read model for a disciplinary case, including all sub-entity detail and collection summaries.
/// </summary>
public class StaffDisciplinaryActionDto : BaseDto
{
    public Guid TenantId { get; set; }
    public string CaseNumber { get; set; } = string.Empty;

    public DisciplinaryStatus Status { get; set; }
    public string StatusName => Status.ToString();

    // Employee
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public string? EmployeeNumber { get; set; }
    public string? EmployeeDepartment { get; set; }

    // Offense
    public Guid StaffOffenseId { get; set; }
    public string OffenseName { get; set; } = string.Empty;
    public string? OffenseCode { get; set; }
    public StaffOffenseSeverity Severity { get; set; }
    public string SeverityName => Severity.ToString();

    // Incident
    public DateTime IncidentDate { get; set; }
    public string IncidentDescription { get; set; } = string.Empty;

    // Reporting
    public Guid ReportedById { get; set; }
    public string ReportedByName { get; set; } = string.Empty;
    public DateTime ReportedDate { get; set; }
    public Guid? ReportedToId { get; set; }
    public string? ReportedToName { get; set; }

    // Process flags
    public bool RequiresInvestigation { get; set; }
    public bool HearingRequired { get; set; }

    // Decision
    public Guid? ActionTypeId { get; set; }
    public string? ActionTypeName { get; set; }
    public string? ActionDetails { get; set; }
    public DateTime? DecisionDate { get; set; }
    public Guid? DecisionById { get; set; }
    public string? DecisionByName { get; set; }
    public string? DecisionRationale { get; set; }

    // Closure
    public DateTime? ClosedDate { get; set; }
    public string? ClosureNotes { get; set; }
    public Guid? ClosedById { get; set; }
    public string? ClosedByName { get; set; }

    // Derived penalty presence flags (computed from sub-entity presence)
    public bool HasWarning { get; set; }
    public bool HasSuspension { get; set; }
    public bool HasFine { get; set; }
    public bool HasTermination { get; set; }
    public bool HasDemotion { get; set; }

    // One-to-one sub-entity details (null when not applicable)
    public StaffDisciplineInvestigationDto? Investigation { get; set; }
    public StaffDisciplineHearingDto? Hearing { get; set; }
    public StaffDisciplineWarningDto? Warning { get; set; }
    public StaffDisciplineSuspensionDto? Suspension { get; set; }
    public StaffDisciplineFineDto? Fine { get; set; }
    public StaffDisciplineTerminationDto? Termination { get; set; }
    public StaffDisciplineSeparationDto? Separation { get; set; }
    public StaffDisciplineAppealDto? Appeal { get; set; }
    public StaffDisciplineCorrectiveActionDto? CorrectiveAction { get; set; }

    // Collections
    public List<StaffDisciplineActionStepDto> ActionSteps { get; set; } = new();
    public List<StaffDisciplineWitnessSummaryDto> Witnesses { get; set; } = new();
    public List<StaffDisciplineDocumentSummaryDto> Documents { get; set; } = new();
    public List<StaffDisciplineNoteSummaryDto> Notes { get; set; } = new();
    public List<StaffDisciplineNotificationSummaryDto> Notifications { get; set; } = new();
    public List<StaffDisciplineLegalReviewSummaryDto> LegalReviews { get; set; } = new();
}

/// <summary>Slim read model for case list views.</summary>
public class StaffDisciplinaryActionSummaryDto
{
    public Guid Id { get; set; }
    public string CaseNumber { get; set; } = string.Empty;
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public string? EmployeeNumber { get; set; }
    public string OffenseName { get; set; } = string.Empty;
    public StaffOffenseSeverity Severity { get; set; }
    public string SeverityName => Severity.ToString();
    public DisciplinaryStatus Status { get; set; }
    public string StatusName => Status.ToString();
    public DateTime IncidentDate { get; set; }
    public DateTime ReportedDate { get; set; }
    public bool HasWarning { get; set; }
    public bool HasSuspension { get; set; }
    public bool HasFine { get; set; }
    public bool HasTermination { get; set; }
    public bool AppealFiled { get; set; }
    public DateTime? ClosedDate { get; set; }
}

public class CreateStaffDisciplinaryActionDto : CreateDtoBase
{
    [Required]
    public Guid EmployeeId { get; set; }

    [Required]
    public Guid StaffOffenseId { get; set; }

    [Required]
    public StaffOffenseSeverity Severity { get; set; }

    [Required]
    public DateTime IncidentDate { get; set; }

    [Required]
    [MaxLength(4000)]
    public string IncidentDescription { get; set; } = string.Empty;

    /// <summary>
    /// Who reported the allegation. Optional: the server fills it from the caller's token, and only
    /// HR may name someone else (recording a report made to them). Left unset by every other caller.
    /// </summary>
    public Guid ReportedById { get; set; }

    [Required]
    public DateTime ReportedDate { get; set; }

    public Guid? ReportedToId { get; set; }

    [MaxLength(50)]
    public string CaseNumber { get; set; } = string.Empty;

    public bool RequiresInvestigation { get; set; }
    public bool HearingRequired { get; set; }
}

public class UpdateStaffDisciplinaryActionDto : UpdateDtoBase
{
    [Required]
    public Guid StaffOffenseId { get; set; }

    [Required]
    public StaffOffenseSeverity Severity { get; set; }

    [Required]
    public DateTime IncidentDate { get; set; }

    [Required]
    [MaxLength(4000)]
    public string IncidentDescription { get; set; } = string.Empty;

    /// <summary>
    /// See <see cref="CreateStaffDisciplinaryActionDto.ReportedById"/> — server-filled from the
    /// token unless HR names someone else.
    /// </summary>
    public Guid ReportedById { get; set; }

    [Required]
    public DateTime ReportedDate { get; set; }

    public Guid? ReportedToId { get; set; }
    public bool RequiresInvestigation { get; set; }
    public bool HearingRequired { get; set; }
}

/// <summary>
/// Records the final decision and action type for a case.
/// </summary>
/// <remarks>
/// <c>DecisionById</c> is deliberately absent: who decided a disciplinary case is testimony, and the
/// server takes it from the caller's token. Accepting it here let any authenticated caller record a
/// decision in someone else's name.
/// </remarks>
public class RecordDisciplinaryDecisionDto
{
    [Required]
    public Guid CaseId { get; set; }

    [Required]
    public Guid ActionTypeId { get; set; }

    [MaxLength(1000)]
    public string? ActionDetails { get; set; }

    [MaxLength(4000)]
    public string? DecisionRationale { get; set; }

    public DateTime DecisionDate { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// How a case stands against the two statutory clocks: FR-HR-177's 48-hour written query and
/// FR-HR-178's four-week investigation.
/// </summary>
/// <remarks>
/// Reported, not enforced. A breach is a fact about what already happened and refusing the next step
/// cannot undo it — see <c>DisciplineProcessDeadlines</c> for why both clocks advise rather than
/// block. The figures are computed from the record on every read, so they cannot drift from the rule.
/// </remarks>
public class DisciplineProcessClockDto
{
    // ── FR-HR-177: the written query ──
    public DateTime QueryDueAt { get; set; }
    public DateTime? QueryIssuedAt { get; set; }
    public bool QueryIssued { get; set; }
    public bool QueryAcknowledged { get; set; }
    public DateTime? QueryAcknowledgedAt { get; set; }

    /// <summary>True once the deadline has passed with no query issued, or it was issued late.</summary>
    public bool QueryBreached { get; set; }

    /// <summary>
    /// Hours late, when breached. Measured to issuance where a query was issued late, and to now
    /// where none has been issued at all — an open breach keeps growing, which is the point.
    /// </summary>
    public double? QueryHoursLate { get; set; }

    // ── FR-HR-178: the investigation ──
    public bool InvestigationRequired { get; set; }
    public bool InvestigationOpened { get; set; }
    public DateTime? InvestigationStartedAt { get; set; }
    public DateTime? InvestigationDueAt { get; set; }
    public DateTime? InvestigationCompletedAt { get; set; }
    public bool InvestigationBreached { get; set; }
    public int? InvestigationDaysLate { get; set; }

    /// <summary>A short sentence per live breach, for the banner. Empty when the case is on time.</summary>
    public List<string> Advisories { get; set; } = new();
}

/// <summary>
/// Comments carried with an approve, refuse or recall of a proposed decision.
/// </summary>
/// <remarks>
/// The actor is not on here for the same reason it is not on the decision payload: the engine takes
/// it from the token, and it is the engine — not the caller — that decides whether they are assigned
/// to the current step.
/// </remarks>
public class DisciplinaryDecisionActionDto
{
    [MaxLength(2000)]
    public string? Comments { get; set; }
}

/// <summary>
/// Transitions a case to Closed status.
/// </summary>
/// <remarks>
/// <c>ClosedById</c> is deliberately absent — server-stamped from the token, for the same reason as
/// <see cref="RecordDisciplinaryDecisionDto"/>.
/// </remarks>
public class CloseDisciplinaryCaseDto
{
    [Required]
    public Guid CaseId { get; set; }

    public DateTime ClosedDate { get; set; } = DateTime.UtcNow;

    [MaxLength(4000)]
    public string? ClosureNotes { get; set; }
}

#endregion

// ============================================================================
// STAFF DISCIPLINE INVESTIGATION DTOs
// ============================================================================

#region Staff Discipline Investigation

public class StaffDisciplineInvestigationDto : BaseDto
{
    public Guid DisciplinaryActionId { get; set; }
    public string CaseNumber { get; set; } = string.Empty;
    public Guid? InvestigatorId { get; set; }
    public string? InvestigatorName { get; set; }
    public DateTime? InvestigationStartDate { get; set; }
    public DateTime? InvestigationEndDate { get; set; }
    public string? InvestigationFindings { get; set; }
    public string? EvidenceCollected { get; set; }
}

public class OpenInvestigationDto
{
    [Required]
    public Guid CaseId { get; set; }

    public Guid? InvestigatorId { get; set; }
    public DateTime? InvestigationStartDate { get; set; }
}

public class UpdateInvestigationDto
{
    [Required]
    public Guid CaseId { get; set; }

    public Guid? InvestigatorId { get; set; }
    public DateTime? InvestigationStartDate { get; set; }
    public DateTime? InvestigationEndDate { get; set; }

    [MaxLength(4000)]
    public string? InvestigationFindings { get; set; }

    [MaxLength(4000)]
    public string? EvidenceCollected { get; set; }
}

#endregion

// ============================================================================
// STAFF DISCIPLINE HEARING DTOs
// ============================================================================

#region Staff Discipline Hearing

public class StaffDisciplineHearingDto : BaseDto
{
    public Guid DisciplinaryActionId { get; set; }
    public string CaseNumber { get; set; } = string.Empty;
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public DateTime? HearingDate { get; set; }
    public string? HearingVenue { get; set; }
    public string? HearingNotes { get; set; }
    public Guid? HearingOfficerId { get; set; }
    public string? HearingOfficerName { get; set; }
    public bool EmployeeAttendedHearing { get; set; }
    public string? EmployeeStatement { get; set; }
    public bool EmployeeHadRepresentation { get; set; }
    public DisciplinaryRepresentativeType? RepresentativeType { get; set; }
    public string? RepresentativeTypeName => RepresentativeType?.ToString();
    public Guid? RepresentativeEmployeeId { get; set; }
    public string? RepresentativeEmployeeName { get; set; }
    public string? RepresentativeName { get; set; }
    public string? RepresentativePosition { get; set; }
    public string? RepresentativeContactInfo { get; set; }
}

public class ScheduleHearingDto
{
    [Required]
    public Guid CaseId { get; set; }

    [Required]
    public DateTime HearingDate { get; set; }

    [MaxLength(500)]
    public string? HearingVenue { get; set; }

    public Guid? HearingOfficerId { get; set; }
}

public class RecordHearingOutcomeDto
{
    [Required]
    public Guid CaseId { get; set; }

    public bool EmployeeAttendedHearing { get; set; }

    [MaxLength(4000)]
    public string? EmployeeStatement { get; set; }

    public bool EmployeeHadRepresentation { get; set; }

    public DisciplinaryRepresentativeType? RepresentativeType { get; set; }
    public Guid? RepresentativeEmployeeId { get; set; }

    [MaxLength(100)]
    public string? RepresentativeName { get; set; }

    [MaxLength(200)]
    public string? RepresentativePosition { get; set; }

    [MaxLength(100)]
    public string? RepresentativeContactInfo { get; set; }

    [MaxLength(4000)]
    public string? HearingNotes { get; set; }
}

#endregion

// ============================================================================
// STAFF DISCIPLINE WARNING DTOs
// ============================================================================

#region Staff Discipline Warning

public class StaffDisciplineWarningDto : BaseDto
{
    public Guid DisciplinaryActionId { get; set; }
    public string CaseNumber { get; set; } = string.Empty;
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public DisciplinaryWarningType WarningType { get; set; }
    public string WarningTypeName => WarningType.ToString();
    public DateTime? WarningExpiryDate { get; set; }
    public bool IsExpired => WarningExpiryDate.HasValue && WarningExpiryDate.Value < DateTime.UtcNow;
    public string? WarningLetterReference { get; set; }
}

public class RecordWarningPenaltyDto
{
    [Required]
    public Guid CaseId { get; set; }

    [Required]
    public DisciplinaryWarningType WarningType { get; set; }

    public DateTime? WarningExpiryDate { get; set; }

    [MaxLength(200)]
    public string? WarningLetterReference { get; set; }
}

public class UpdateWarningPenaltyDto
{
    [Required]
    public Guid CaseId { get; set; }

    [Required]
    public DisciplinaryWarningType WarningType { get; set; }

    public DateTime? WarningExpiryDate { get; set; }

    [MaxLength(200)]
    public string? WarningLetterReference { get; set; }
}

#endregion

// ============================================================================
// STAFF DISCIPLINE SUSPENSION DTOs
// ============================================================================

#region Staff Discipline Suspension

public class StaffDisciplineSuspensionDto : BaseDto
{
    public Guid DisciplinaryActionId { get; set; }
    public string CaseNumber { get; set; } = string.Empty;
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public DateTime? SuspensionStartDate { get; set; }
    public DateTime? SuspensionEndDate { get; set; }
    public bool SuspensionWithPay { get; set; }
    public int? SuspensionDays => SuspensionStartDate.HasValue && SuspensionEndDate.HasValue
        ? (int)(SuspensionEndDate.Value - SuspensionStartDate.Value).TotalDays
        : null;
}

public class RecordSuspensionPenaltyDto
{
    [Required]
    public Guid CaseId { get; set; }

    public DateTime? SuspensionStartDate { get; set; }
    public DateTime? SuspensionEndDate { get; set; }
    public bool SuspensionWithPay { get; set; }
}

public class UpdateSuspensionPenaltyDto
{
    [Required]
    public Guid CaseId { get; set; }

    public DateTime? SuspensionStartDate { get; set; }
    public DateTime? SuspensionEndDate { get; set; }
    public bool SuspensionWithPay { get; set; }
}

#endregion

// ============================================================================
// STAFF DISCIPLINE FINE DTOs
// ============================================================================

#region Staff Discipline Fine

public class StaffDisciplineFineDto : BaseDto
{
    public Guid DisciplinaryActionId { get; set; }
    public string CaseNumber { get; set; } = string.Empty;
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public decimal? FineAmount { get; set; }
    public DisciplinaryFinePaymentStatus? FinePaymentStatus { get; set; }
    public string? FinePaymentStatusName => FinePaymentStatus?.ToString();
    public DateTime? FineDueDate { get; set; }
    public decimal? FinePaidAmount { get; set; }
    public DateTime? FinePaymentDate { get; set; }
    public decimal? OutstandingBalance => FineAmount.HasValue
        ? FineAmount.Value - (FinePaidAmount ?? 0m)
        : null;
}

public class RecordFinePenaltyDto
{
    [Required]
    public Guid CaseId { get; set; }

    [Required]
    [Range(0.01, double.MaxValue)]
    public decimal FineAmount { get; set; }

    public DateTime? FineDueDate { get; set; }
}

public class RecordFinePaymentDto
{
    [Required]
    public Guid CaseId { get; set; }

    [Required]
    [Range(0.01, double.MaxValue)]
    public decimal AmountPaid { get; set; }

    public DateTime PaymentDate { get; set; } = DateTime.UtcNow;

    [Required]
    public DisciplinaryFinePaymentStatus PaymentStatus { get; set; }
}

#endregion

// ============================================================================
// STAFF DISCIPLINE ACTION STEP DTOs
// ============================================================================

#region Staff Discipline Action Step

public class StaffDisciplineActionStepDto : BaseDto
{
    public Guid DisciplinaryActionId { get; set; }
    public Guid OffenseProcedureId { get; set; }
    public string StepName { get; set; } = string.Empty;
    public string? StepDescription { get; set; }
    public int Sequence { get; set; }
    public int? ExpectedCompletionDays { get; set; }
    public DateTime? DueDate { get; set; }
    public DateTime? StartedDate { get; set; }
    public DateTime? CompletedDate { get; set; }
    public DisciplinaryActionStepStatus Status { get; set; }
    public string StatusName => Status.ToString();
    public Guid? ActionedById { get; set; }
    public string? ActionedByName { get; set; }
    public string? Notes { get; set; }
    public bool IsOverdue => DueDate.HasValue && DueDate.Value < DateTime.UtcNow && Status == DisciplinaryActionStepStatus.Pending;
    public List<StaffDisciplineDocumentSummaryDto> Documents { get; set; } = new();
}

public class UpdateActionStepDto
{
    [Required]
    public Guid StepId { get; set; }

    public DateTime? DueDate { get; set; }
    public DateTime? StartedDate { get; set; }
    public DateTime? CompletedDate { get; set; }

    [Required]
    public DisciplinaryActionStepStatus Status { get; set; }

    public Guid? ActionedById { get; set; }

    [MaxLength(2000)]
    public string? Notes { get; set; }
}

#endregion

// ============================================================================
// STAFF DISCIPLINE WITNESS DTOs
// ============================================================================

#region Staff Discipline Witness

public class StaffDisciplineWitnessDto : BaseDto
{
    public Guid TenantId { get; set; }
    public Guid DisciplinaryActionId { get; set; }
    public string CaseNumber { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsEmployee { get; set; }
    public Guid? EmployeeId { get; set; }
    public string? EmployeeName { get; set; }
    public string? ContactInfo { get; set; }
    public string? Statement { get; set; }
    public DateTime? StatementDate { get; set; }
}

public class StaffDisciplineWitnessSummaryDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsEmployee { get; set; }
    public Guid? EmployeeId { get; set; }
    public bool HasStatement => !string.IsNullOrEmpty(Statement);
    public string? Statement { get; set; }
    public DateTime? StatementDate { get; set; }
}

public class CreateStaffDisciplineWitnessDto : CreateDtoBase
{
    [Required]
    public Guid DisciplinaryActionId { get; set; }

    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    public bool IsEmployee { get; set; }
    public Guid? EmployeeId { get; set; }

    [MaxLength(200)]
    public string? ContactInfo { get; set; }

    [MaxLength(4000)]
    public string? Statement { get; set; }

    public DateTime? StatementDate { get; set; }
}

public class UpdateStaffDisciplineWitnessDto : UpdateDtoBase
{
    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    public bool IsEmployee { get; set; }
    public Guid? EmployeeId { get; set; }

    [MaxLength(200)]
    public string? ContactInfo { get; set; }

    [MaxLength(4000)]
    public string? Statement { get; set; }

    public DateTime? StatementDate { get; set; }
}

#endregion

// ============================================================================
// STAFF DISCIPLINE DOCUMENT DTOs
// ============================================================================

#region Staff Discipline Document

public class StaffDisciplineDocumentDto : BaseDto
{
    public Guid TenantId { get; set; }
    public Guid DisciplinaryActionId { get; set; }
    public string CaseNumber { get; set; } = string.Empty;
    public DisciplinaryDocumentScope Scope { get; set; }
    public string ScopeName => Scope.ToString();
    public Guid? ActionStepId { get; set; }
    public string? ActionStepName { get; set; }
    public Guid? AppealId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public DisciplinaryDocumentCategory Category { get; set; }
    public string CategoryName => Category.ToString();
    public string? Description { get; set; }
    public DateTime UploadDate { get; set; }
    public Guid UploadedById { get; set; }
    public string UploadedByName { get; set; } = string.Empty;
}

public class StaffDisciplineDocumentSummaryDto
{
    public Guid Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public DisciplinaryDocumentCategory Category { get; set; }
    public string CategoryName => Category.ToString();
    public DisciplinaryDocumentScope Scope { get; set; }
    public string ScopeName => Scope.ToString();
    public Guid? ActionStepId { get; set; }
    public Guid? AppealId { get; set; }
    public DateTime UploadDate { get; set; }
    public string UploadedByName { get; set; } = string.Empty;
}

public class CreateStaffDisciplineDocumentDto : CreateDtoBase
{
    [Required]
    public Guid DisciplinaryActionId { get; set; }

    [Required]
    public DisciplinaryDocumentScope Scope { get; set; } = DisciplinaryDocumentScope.Case;

    public Guid? ActionStepId { get; set; }
    public Guid? AppealId { get; set; }

    [Required]
    [MaxLength(500)]
    public string FileName { get; set; } = string.Empty;

    /// <summary>
    /// Legacy storage path. Set only by the migration utility for pre-existing rows — the
    /// upload endpoint leaves it empty and uses <see cref="FileUploadRecordId"/>. An API
    /// caller supplying this would be choosing which bytes on disk a document points at,
    /// so the controller rejects it.
    /// </summary>
    [MaxLength(1000)]
    public string FilePath { get; set; } = string.Empty;

    /// <summary>Scanned controlled upload backing this document.</summary>
    public Guid? FileUploadRecordId { get; set; }

    /// <summary>Central-DMS record, once registered.</summary>
    public Guid? DocumentRecordId { get; set; }

    /// <summary>Central-DMS version, once registered.</summary>
    public Guid? DocumentVersionId { get; set; }

    [Required]
    public DisciplinaryDocumentCategory Category { get; set; }

    [MaxLength(500)]
    public string? Description { get; set; }

    [Required]
    public Guid UploadedById { get; set; }

    public DateTime UploadDate { get; set; } = DateTime.UtcNow;
}

#endregion

// ============================================================================
// STAFF DISCIPLINE NOTE DTOs
// ============================================================================

#region Staff Discipline Note

public class StaffDisciplineNoteDto : BaseDto
{
    public Guid TenantId { get; set; }
    public Guid DisciplinaryActionId { get; set; }
    public string CaseNumber { get; set; } = string.Empty;
    public Guid CreatedByEmployeeId { get; set; }
    public string CreatedByEmployeeName { get; set; } = string.Empty;
    public string Note { get; set; } = string.Empty;
    public bool IsConfidential { get; set; }
    public DateTime NoteDate { get; set; }
}

public class StaffDisciplineNoteSummaryDto
{
    public Guid Id { get; set; }
    public string CreatedByEmployeeName { get; set; } = string.Empty;
    public string NoteExcerpt { get; set; } = string.Empty;
    public bool IsConfidential { get; set; }
    public DateTime NoteDate { get; set; }
}

public class CreateStaffDisciplineNoteDto : CreateDtoBase
{
    [Required]
    public Guid DisciplinaryActionId { get; set; }

    [Required]
    public Guid CreatedByEmployeeId { get; set; }

    [Required]
    [MaxLength(4000)]
    public string Note { get; set; } = string.Empty;

    public bool IsConfidential { get; set; }

    public DateTime NoteDate { get; set; } = DateTime.UtcNow;
}

public class UpdateStaffDisciplineNoteDto : UpdateDtoBase
{
    [Required]
    [MaxLength(4000)]
    public string Note { get; set; } = string.Empty;

    public bool IsConfidential { get; set; }
}

#endregion

// ============================================================================
// STAFF DISCIPLINE NOTIFICATION DTOs
// ============================================================================

#region Staff Discipline Notification

public class StaffDisciplineNotificationDto : BaseDto
{
    public Guid TenantId { get; set; }
    public Guid DisciplinaryActionId { get; set; }
    public string CaseNumber { get; set; } = string.Empty;
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public DisciplinaryNotificationType NotificationType { get; set; }
    public string NotificationTypeName => NotificationType.ToString();
    public DateTime SentDate { get; set; }
    public string Content { get; set; } = string.Empty;
    public Guid SentById { get; set; }
    public string SentByName { get; set; } = string.Empty;
    public DateTime? AcknowledgedDate { get; set; }
    public bool IsAcknowledged => AcknowledgedDate.HasValue;
    public bool IsFollowupSent { get; set; }
    public DateTime? FollowupDate { get; set; }
}

public class StaffDisciplineNotificationSummaryDto
{
    public Guid Id { get; set; }
    public DisciplinaryNotificationType NotificationType { get; set; }
    public string NotificationTypeName => NotificationType.ToString();
    public DateTime SentDate { get; set; }
    public bool IsAcknowledged { get; set; }
    public bool IsFollowupSent { get; set; }
}

public class CreateStaffDisciplineNotificationDto : CreateDtoBase
{
    [Required]
    public Guid DisciplinaryActionId { get; set; }

    [Required]
    public DisciplinaryNotificationType NotificationType { get; set; }

    [Required]
    public DateTime SentDate { get; set; }

    [Required]
    [MaxLength(4000)]
    public string Content { get; set; } = string.Empty;

    /// <remarks>
    /// <c>SentById</c> is deliberately absent — server-stamped from the caller's token. Whoever
    /// issues a notice is its sender, and a disciplinary notice is a document whose authorship must
    /// not be forgeable.
    /// </remarks>
}

/// <summary>
/// Records that the subject of a case received a disciplinary notice.
/// </summary>
/// <remarks>
/// <c>AcknowledgedDate</c> is deliberately absent — server-stamped, because the response clocks run
/// from it and a client-supplied date would let them be set to whatever suits.
/// </remarks>
public class AcknowledgeNotificationDto
{
    [Required]
    public Guid NotificationId { get; set; }
}

public class SendFollowupNotificationDto
{
    [Required]
    public Guid NotificationId { get; set; }

    public DateTime FollowupDate { get; set; } = DateTime.UtcNow;
}

#endregion

// ============================================================================
// STAFF DISCIPLINE APPEAL DTOs
// ============================================================================

#region Staff Discipline Appeal

public class StaffDisciplineAppealDto : BaseDto
{
    public Guid TenantId { get; set; }
    public Guid DisciplinaryActionId { get; set; }
    public string CaseNumber { get; set; } = string.Empty;
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public DateTime FiledDate { get; set; }
    public string Reason { get; set; } = string.Empty;
    public DisciplineAppealStatus AppealStatus { get; set; }
    public string AppealStatusName => AppealStatus.ToString();
    public Guid? AppealOfficerId { get; set; }
    public string? AppealOfficerName { get; set; }
    public DateTime? HearingDate { get; set; }
    public string? HearingVenue { get; set; }
    public string? HearingNotes { get; set; }
    public DisciplineAppealOutcomeType? AppealOutcome { get; set; }
    public string? AppealOutcomeName => AppealOutcome?.ToString();
    public string? AppealOutcomeNotes { get; set; }
    public DateTime? AppealOutcomeDate { get; set; }
    public Guid? AppealOutcomeById { get; set; }
    public string? AppealOutcomeByName { get; set; }
    public List<StaffDisciplineDocumentSummaryDto> Documents { get; set; } = new();
}

/// <summary>
/// Files an appeal against a decided case.
/// </summary>
/// <remarks>
/// <c>EmployeeId</c> is deliberately absent: an appeal is the subject's own act, so the appellant is
/// the case's employee and the service refuses the call from anyone else — HR included. Accepting an
/// appellant here let any authenticated caller file an appeal in someone else's name.
/// <c>FiledDate</c> is server-stamped for the same reason it is in the demotion-response path: the
/// FR-HR-180 five-working-day window is measured against it, so it cannot come from the client.
/// </remarks>
public class FileAppealDto
{
    [Required]
    public Guid CaseId { get; set; }

    [Required]
    [MaxLength(2000)]
    public string Reason { get; set; } = string.Empty;
}

public class ScheduleAppealHearingDto
{
    [Required]
    public Guid CaseId { get; set; }

    [Required]
    public DateTime HearingDate { get; set; }

    [MaxLength(1000)]
    public string? HearingVenue { get; set; }

    public Guid? AppealOfficerId { get; set; }
}

public class RecordAppealOutcomeDto
{
    [Required]
    public Guid CaseId { get; set; }

    [Required]
    public DisciplineAppealOutcomeType AppealOutcome { get; set; }

    [MaxLength(3000)]
    public string? AppealOutcomeNotes { get; set; }

    public DateTime AppealOutcomeDate { get; set; } = DateTime.UtcNow;

    /// <remarks>
    /// <c>AppealOutcomeById</c> is deliberately absent — who decided an appeal is testimony, taken
    /// from the caller's token.
    /// </remarks>
    [MaxLength(3000)]
    public string? HearingNotes { get; set; }
}

#endregion

// ============================================================================
// STAFF DISCIPLINE CORRECTIVE ACTION DTOs
// ============================================================================

#region Staff Discipline Corrective Action

public class StaffDisciplineCorrectiveActionDto : BaseDto
{
    public Guid TenantId { get; set; }
    public Guid DisciplinaryActionId { get; set; }
    public string CaseNumber { get; set; } = string.Empty;
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public Guid SupervisorId { get; set; }
    public string SupervisorName { get; set; } = string.Empty;
    public string Objective { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime ReviewDate { get; set; }
    public DateTime? CompletedDate { get; set; }
    public DisciplineCorrectiveActionStatus Status { get; set; }
    public string StatusName => Status.ToString();
    public string? Notes { get; set; }
    public bool IsOverdue => ReviewDate < DateTime.UtcNow && Status != DisciplineCorrectiveActionStatus.Completed && Status != DisciplineCorrectiveActionStatus.Cancelled;
    public int ItemCount { get; set; }
    public int CompletedItemCount { get; set; }
    public List<StaffDisciplineCorrectiveActionItemDto> Items { get; set; } = new();
}

public class CreateStaffDisciplineCorrectiveActionDto : CreateDtoBase
{
    [Required]
    public Guid DisciplinaryActionId { get; set; }

    [Required]
    public Guid EmployeeId { get; set; }

    [Required]
    public Guid SupervisorId { get; set; }

    [Required]
    [MaxLength(1000)]
    public string Objective { get; set; } = string.Empty;

    [Required]
    public DateTime StartDate { get; set; }

    [Required]
    public DateTime ReviewDate { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }
}

public class UpdateStaffDisciplineCorrectiveActionDto : UpdateDtoBase
{
    [Required]
    public Guid SupervisorId { get; set; }

    [Required]
    [MaxLength(1000)]
    public string Objective { get; set; } = string.Empty;

    [Required]
    public DateTime StartDate { get; set; }

    [Required]
    public DateTime ReviewDate { get; set; }

    public DateTime? CompletedDate { get; set; }

    [Required]
    public DisciplineCorrectiveActionStatus Status { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }
}

#endregion

// ============================================================================
// STAFF DISCIPLINE CORRECTIVE ACTION ITEM DTOs
// ============================================================================

#region Staff Discipline Corrective Action Item

public class StaffDisciplineCorrectiveActionItemDto : BaseDto
{
    public Guid CorrectiveActionId { get; set; }
    public string Description { get; set; } = string.Empty;
    public DateTime TargetDate { get; set; }
    public DateTime? CompletedDate { get; set; }
    public DisciplineCorrectiveActionStatus Status { get; set; }
    public string StatusName => Status.ToString();
    public string? CompletionNotes { get; set; }
    public bool IsOverdue => TargetDate < DateTime.UtcNow && Status != DisciplineCorrectiveActionStatus.Completed && Status != DisciplineCorrectiveActionStatus.Cancelled;
}

public class CreateStaffDisciplineCorrectiveActionItemDto : CreateDtoBase
{
    [Required]
    public Guid CorrectiveActionId { get; set; }

    [Required]
    [MaxLength(1000)]
    public string Description { get; set; } = string.Empty;

    [Required]
    public DateTime TargetDate { get; set; }
}

public class UpdateStaffDisciplineCorrectiveActionItemDto : UpdateDtoBase
{
    [Required]
    [MaxLength(1000)]
    public string Description { get; set; } = string.Empty;

    [Required]
    public DateTime TargetDate { get; set; }

    public DateTime? CompletedDate { get; set; }

    [Required]
    public DisciplineCorrectiveActionStatus Status { get; set; }

    [MaxLength(1000)]
    public string? CompletionNotes { get; set; }
}

#endregion

// ============================================================================
// STAFF DISCIPLINE LEGAL REVIEW DTOs
// ============================================================================

#region Staff Discipline Legal Review

public class StaffDisciplineLegalReviewDto : BaseDto
{
    public Guid TenantId { get; set; }
    public Guid DisciplinaryActionId { get; set; }
    public string CaseNumber { get; set; } = string.Empty;
    public DateTime ReferredToLegalDate { get; set; }
    public Guid? ReferredById { get; set; }
    public string? ReferredByName { get; set; }
    public DateTime? LegalReviewCompleteDate { get; set; }
    public DisciplineLegalRiskLevel LegalRiskLevel { get; set; }
    public string LegalRiskLevelName => LegalRiskLevel.ToString();
    public string? LegalAdvice { get; set; }
    public bool RequiresExternalCounsel { get; set; }
    public Guid? ExternalCounselId { get; set; }
    public string? ExternalCounselName { get; set; }
    public string? ExternalCounselFirm { get; set; }
    public DateTime? ExternalCounselReviewDate { get; set; }
    public string? ExternalCounselOpinion { get; set; }
    public decimal? LegalCostsIncurred { get; set; }
    public bool IsConfidential { get; set; }
}

public class StaffDisciplineLegalReviewSummaryDto
{
    public Guid Id { get; set; }
    public DateTime ReferredToLegalDate { get; set; }
    public DisciplineLegalRiskLevel LegalRiskLevel { get; set; }
    public string LegalRiskLevelName => LegalRiskLevel.ToString();
    public DateTime? LegalReviewCompleteDate { get; set; }
    public bool RequiresExternalCounsel { get; set; }
    public bool IsConfidential { get; set; }
}

public class CreateStaffDisciplineLegalReviewDto : CreateDtoBase
{
    [Required]
    public Guid DisciplinaryActionId { get; set; }

    [Required]
    public DateTime ReferredToLegalDate { get; set; }

    public Guid? ReferredById { get; set; }

    [Required]
    public DisciplineLegalRiskLevel LegalRiskLevel { get; set; }

    public bool RequiresExternalCounsel { get; set; }
    public Guid? ExternalCounselId { get; set; }

    [MaxLength(200)]
    public string? ExternalCounselName { get; set; }

    [MaxLength(700)]
    public string? ExternalCounselFirm { get; set; }

    public bool IsConfidential { get; set; } = true;
}

public class UpdateStaffDisciplineLegalReviewDto : UpdateDtoBase
{
    [Required]
    public DisciplineLegalRiskLevel LegalRiskLevel { get; set; }

    public DateTime? LegalReviewCompleteDate { get; set; }

    [MaxLength(4000)]
    public string? LegalAdvice { get; set; }

    public bool RequiresExternalCounsel { get; set; }
    public Guid? ExternalCounselId { get; set; }

    [MaxLength(200)]
    public string? ExternalCounselName { get; set; }

    [MaxLength(700)]
    public string? ExternalCounselFirm { get; set; }

    public DateTime? ExternalCounselReviewDate { get; set; }

    [MaxLength(4000)]
    public string? ExternalCounselOpinion { get; set; }

    [Range(0, double.MaxValue)]
    public decimal? LegalCostsIncurred { get; set; }

    public bool IsConfidential { get; set; }
}

#endregion

// ============================================================================
// STAFF DISCIPLINE TERMINATION DTOs
// ============================================================================

#region Staff Discipline Termination

public class StaffDisciplineTerminationDto : BaseDto
{
    public Guid DisciplinaryActionId { get; set; }
    public string CaseNumber { get; set; } = string.Empty;
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public EmployeeTerminationType Type { get; set; }
    public string TypeName => Type.ToString();
    public bool IsEligibleForRehire { get; set; }
    public DateTime? EligibleForRehireDate { get; set; }
    public string? RehireRestrictions { get; set; }
    public bool FinalPaycheckProcessed { get; set; }
    public DateTime? FinalPaycheckDate { get; set; }
    public decimal? FinalPaycheckAmount { get; set; }
    public string? SeparationNotes { get; set; }
}

public class RecordTerminationDto
{
    [Required]
    public Guid CaseId { get; set; }

    [Required]
    public EmployeeTerminationType Type { get; set; }

    public bool IsEligibleForRehire { get; set; }
    public DateTime? EligibleForRehireDate { get; set; }

    [MaxLength(1000)]
    public string? RehireRestrictions { get; set; }

    [MaxLength(4000)]
    public string? SeparationNotes { get; set; }
}

public class UpdateTerminationDto
{
    [Required]
    public Guid CaseId { get; set; }

    [Required]
    public EmployeeTerminationType Type { get; set; }

    public bool IsEligibleForRehire { get; set; }
    public DateTime? EligibleForRehireDate { get; set; }

    [MaxLength(1000)]
    public string? RehireRestrictions { get; set; }

    public bool FinalPaycheckProcessed { get; set; }
    public DateTime? FinalPaycheckDate { get; set; }

    [Range(0, double.MaxValue)]
    public decimal? FinalPaycheckAmount { get; set; }

    [MaxLength(4000)]
    public string? SeparationNotes { get; set; }
}

#endregion

// ============================================================================
// STAFF DISCIPLINE SEPARATION DTOs
// ============================================================================

#region Staff Discipline Separation

public class StaffDisciplineSeparationDto : BaseDto
{
    public Guid DisciplinaryActionId { get; set; }
    public string CaseNumber { get; set; } = string.Empty;
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public bool ExitInterviewCompleted { get; set; }
    public DateTime? ExitInterviewDate { get; set; }
    public string? ExitInterviewNotes { get; set; }
    public Guid? ExitInterviewerId { get; set; }
    public string? ExitInterviewerName { get; set; }
    public bool EquipmentReturned { get; set; }
    public DateTime? EquipmentReturnedDate { get; set; }
    public string? MissingEquipment { get; set; }
    public bool AccessRevoked { get; set; }
    public DateTime? AccessRevokedDate { get; set; }
    public Guid? AccessRevokedById { get; set; }
    public string? AccessRevokedByName { get; set; }
    public bool FinalPayrollProcessed { get; set; }
    public DateTime? FinalPayrollDate { get; set; }
    public bool BenefitsTerminated { get; set; }
    public DateTime? BenefitsTerminationDate { get; set; }
    public bool ExitChecklistCompleted { get; set; }
    public DateTime? ExitChecklistCompletedDate { get; set; }
    public string? AdditionalNotes { get; set; }
    public int ChecklistCompletionPercent { get; set; }
}

public class InitiateSeparationDto
{
    [Required]
    public Guid CaseId { get; set; }

    public Guid? ExitInterviewerId { get; set; }
}

public class UpdateSeparationDto
{
    [Required]
    public Guid CaseId { get; set; }

    public bool ExitInterviewCompleted { get; set; }
    public DateTime? ExitInterviewDate { get; set; }

    [MaxLength(4000)]
    public string? ExitInterviewNotes { get; set; }

    public Guid? ExitInterviewerId { get; set; }
    public bool EquipmentReturned { get; set; }
    public DateTime? EquipmentReturnedDate { get; set; }

    [MaxLength(1000)]
    public string? MissingEquipment { get; set; }

    public bool AccessRevoked { get; set; }
    public DateTime? AccessRevokedDate { get; set; }
    public Guid? AccessRevokedById { get; set; }
    public bool FinalPayrollProcessed { get; set; }
    public DateTime? FinalPayrollDate { get; set; }
    public bool BenefitsTerminated { get; set; }
    public DateTime? BenefitsTerminationDate { get; set; }
    public bool ExitChecklistCompleted { get; set; }
    public DateTime? ExitChecklistCompletedDate { get; set; }

    [MaxLength(4000)]
    public string? AdditionalNotes { get; set; }
}

#endregion

// ============================================================================
// STAFF DISCIPLINE DASHBOARD
// ============================================================================

#region Staff Discipline Dashboard

/// <summary>
/// Aggregated metrics for the disciplinary module dashboard.
/// All counters are computed server-side and returned in a single request.
/// </summary>
public class StaffDisciplineDashboardDto
{
    // ── Open case counters ───────────────────────────────────────────────────
    public int TotalOpenCases { get; set; }
    public int CasesUnderInvestigation { get; set; }
    public int CasesAwaitingHearing { get; set; }
    public int CasesAwaitingDecision { get; set; }
    public int CasesWithActiveAppeal { get; set; }
    public int CasesClosedThisMonth { get; set; }

    // ── Penalty breakdown (open cases) ───────────────────────────────────────
    public int ActiveWarnings { get; set; }
    public int ActiveSuspensions { get; set; }
    public int ActiveFines { get; set; }
    public int TerminationsPendingProcessing { get; set; }

    // ── Severity distribution ────────────────────────────────────────────────
    public int MinorCases { get; set; }
    public int ModerateCases { get; set; }
    public int SeriousCases { get; set; }
    public int GrossMisconductCases { get; set; }

    // ── Overdue items ────────────────────────────────────────────────────────
    public int OverdueActionSteps { get; set; }
    public int OverdueCorrectiveActionItems { get; set; }
    public int OutstandingFines { get; set; }

    // ── Recent case alerts (capped for dashboard) ────────────────────────────
    public List<DisciplinaryCaseAlertDto> RecentOpenCases { get; set; } = new();
    public List<DisciplinaryCaseAlertDto> OverdueCases { get; set; } = new();

    // ── Meta ─────────────────────────────────────────────────────────────────
    public DateTime ComputedAt { get; set; } = DateTime.UtcNow;
}

public class DisciplinaryCaseAlertDto
{
    public Guid CaseId { get; set; }
    public string CaseNumber { get; set; } = string.Empty;
    public string EmployeeName { get; set; } = string.Empty;
    public string OffenseName { get; set; } = string.Empty;
    public StaffOffenseSeverity Severity { get; set; }
    public string SeverityName => Severity.ToString();
    public DisciplinaryStatus Status { get; set; }
    public string StatusName => Status.ToString();
    public DateTime IncidentDate { get; set; }
    public int DaysOpen { get; set; }
}

#endregion
