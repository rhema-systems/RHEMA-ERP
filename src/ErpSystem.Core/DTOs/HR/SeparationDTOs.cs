using System.ComponentModel.DataAnnotations;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.DTOs.HR;

// =============================================================================
// AREA 9b — SEPARATION, CLEARANCE & EXIT (slice 1: the record and the register)
// =============================================================================

/// <summary>One row of the exit register.</summary>
public class EmployeeSeparationListDto
{
    public Guid Id { get; set; }
    public string SeparationNumber { get; set; } = string.Empty;

    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public string? EmployeeNumber { get; set; }
    public string? PositionTitle { get; set; }
    public string? OrganizationUnitName { get; set; }

    public EmployeeTerminationType SeparationType { get; set; }
    public string SeparationTypeName { get; set; } = string.Empty;

    public SeparationStatus Status { get; set; }
    public string StatusName { get; set; } = string.Empty;

    public DateOnly InitiatedOn { get; set; }
    public DateOnly? LastWorkingDay { get; set; }
    public DateOnly? EffectiveDate { get; set; }

    public bool IsProcedural { get; set; }
    public bool IsSystemInitiated { get; set; }

    /// <summary>True when this separation came out of a disciplinary case.</summary>
    public bool IsDisciplinary { get; set; }

    /// <summary>
    /// True when the outcome has been applied to the employee's master record. False on a
    /// completed separation is the 29-orphan defect, visible without a join.
    /// </summary>
    public bool EmployeeRecordUpdated { get; set; }
}

/// <summary>A separation in full.</summary>
public class EmployeeSeparationDetailDto : EmployeeSeparationListDto
{
    public TerminationReason? ReasonCategory { get; set; }
    public string? ReasonCategoryName { get; set; }
    public string? ReasonNotes { get; set; }

    public DateOnly? NoticeGivenOn { get; set; }
    public int? NoticeDays { get; set; }

    /// <summary>
    /// Notice the separation type calls for — the value on the record, which was defaulted from
    /// tenant policy when it was raised.
    /// </summary>
    public int NoticeRequiredDays { get; set; }

    /// <summary>
    /// Notice actually served: <c>NoticeGivenOn</c> to <c>LastWorkingDay</c>. Null while either
    /// date is unknown.
    /// </summary>
    public int? NoticeServedDays { get; set; }

    /// <summary>
    /// How much notice was not served — what FR-HR-184's notice pay is computed from in slice 5.
    /// </summary>
    /// <remarks>
    /// Derived on read, never stored. Its three inputs are all persisted and stop being editable
    /// once the separation leaves Draft, so storing the answer as well would only create something
    /// that can drift from them.
    /// </remarks>
    public int? NoticeShortfallDays { get; set; }

    public DateTime? SubmittedOn { get; set; }
    public Guid? SubmittedById { get; set; }
    public string? SubmittedByName { get; set; }

    public Guid? InitiatedById { get; set; }
    public string? InitiatedByName { get; set; }

    public Guid? ApprovedById { get; set; }
    public string? ApprovedByName { get; set; }
    public DateTime? ApprovedOn { get; set; }
    public string? ApprovalNotes { get; set; }
    public Guid? WorkflowInstanceId { get; set; }

    public Guid? RejectedById { get; set; }
    public string? RejectedByName { get; set; }
    public DateTime? RejectedOn { get; set; }
    public string? RejectionReason { get; set; }

    /// <summary>Days of unauthorised absence, where this is an absence-based termination.</summary>
    public int? AbsenceDays { get; set; }

    /// <summary>
    /// True when the Managing Director's signature is required (FR-HR-092) — which is everything
    /// except a procedural separation. Derived from <c>IsProcedural</c>, and exposed so a client can
    /// route the record to the right person instead of finding out by being refused.
    /// </summary>
    public bool RequiresManagingDirectorSignature { get; set; }

    public bool IsNoticeWaived { get; set; }
    public string? NoticeWaiverReason { get; set; }
    public bool IsNoticePaidInLieu { get; set; }

    public bool IsEligibleForRehire { get; set; }
    public DateOnly? EligibleForRehireDate { get; set; }
    public string? RehireRestrictions { get; set; }

    public Guid? DisciplinaryActionId { get; set; }
    public DateTime? EmployeeRecordUpdatedOn { get; set; }

    public DateTime? CancelledOn { get; set; }
    public Guid? CancelledById { get; set; }
    public string? CancelledByName { get; set; }
    public string? CancellationReason { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

/// <summary>
/// Raise a separation. Status is deliberately absent — a separation always starts as a draft, and
/// every move from there is an endpoint with its own rule and its own gate.
/// </summary>
public class CreateEmployeeSeparationDto
{
    [Required]
    public Guid EmployeeId { get; set; }

    [Required]
    public EmployeeTerminationType SeparationType { get; set; }

    public TerminationReason? ReasonCategory { get; set; }

    [MaxLength(2000)]
    public string? ReasonNotes { get; set; }

    /// <summary>Defaults to today when omitted. Never in the future.</summary>
    public DateOnly? InitiatedOn { get; set; }

    public DateOnly? NoticeGivenOn { get; set; }

    /// <summary>
    /// Omit to take the tenant default from <c>CompanyHrPolicySettings</c> — 30 days for a
    /// resignation, 30 for a termination. Supply a value only to override it for this separation.
    /// </summary>
    public int? NoticeDays { get; set; }

    public DateOnly? LastWorkingDay { get; set; }

    /// <summary>
    /// The intended last day of employment. Confirmed at approval; for compulsory retirement the
    /// service computes it from the birthday and refuses a contradicting value (FR-HR-093).
    /// </summary>
    public DateOnly? EffectiveDate { get; set; }

    public bool IsEligibleForRehire { get; set; } = true;

    public DateOnly? EligibleForRehireDate { get; set; }

    [MaxLength(1000)]
    public string? RehireRestrictions { get; set; }

    /// <summary>
    /// Days of unauthorised absence, where this is an absence-based termination. At or above the
    /// tenant's <c>ProceduralAbsenceDays</c> the separation becomes procedural on submission and HR
    /// may approve it without the MD's signature (FR-HR-092).
    /// </summary>
    public int? AbsenceDays { get; set; }

    /// <summary>
    /// Set when a disciplinary outcome is raising this separation, linking back to the action that
    /// decided it. Area 9 supplies this; a human raising a separation by hand does not.
    /// </summary>
    public Guid? DisciplinaryActionId { get; set; }
}

/// <summary>
/// Amend a draft separation. Anything already decided — approval, clearance, settlement — is not
/// here: those move through their own endpoints so each keeps its rule.
/// </summary>
public class UpdateEmployeeSeparationDto
{
    public EmployeeTerminationType? SeparationType { get; set; }
    public TerminationReason? ReasonCategory { get; set; }

    [MaxLength(2000)]
    public string? ReasonNotes { get; set; }

    public DateOnly? NoticeGivenOn { get; set; }
    public int? NoticeDays { get; set; }
    public DateOnly? LastWorkingDay { get; set; }
    public DateOnly? EffectiveDate { get; set; }

    public bool? IsEligibleForRehire { get; set; }
    public DateOnly? EligibleForRehireDate { get; set; }
    public int? AbsenceDays { get; set; }

    [MaxLength(1000)]
    public string? RehireRestrictions { get; set; }
}

/// <summary>Withdraw a separation before it completes — a resignation retracted, a retirement deferred.</summary>
public class CancelEmployeeSeparationDto
{
    /// <summary>Why the separation is being withdrawn. Required — the service enforces it.</summary>
    /// <remarks>
    /// ⚠ Deliberately no <c>[Required]</c>. It would fire first, in ModelState, and answer with the
    /// framework's "One or more validation errors occurred." — which tells the person staring at
    /// the screen nothing about what to do. The service refuses a blank reason with a sentence that
    /// does, and that keeps every refusal on this controller one shape: <c>{ "message": "…" }</c>.
    /// </remarks>
    [MaxLength(1000)]
    public string Reason { get; set; } = string.Empty;
}

/// <summary>
/// Sign off a separation (FR-HR-092). Who may send this depends on the record, not on a
/// permission: the Managing Director may approve any separation, HR only a procedural one.
/// </summary>
public class ApproveEmployeeSeparationDto
{
    [MaxLength(2000)]
    public string? Notes { get; set; }

    /// <summary>
    /// Release the employee without requiring the balance of their notice, and without recovering
    /// it. Requires a reason, and is refused where no notice is outstanding.
    /// </summary>
    public bool WaiveNotice { get; set; }

    [MaxLength(1000)]
    public string? NoticeWaiverReason { get; set; }

    /// <summary>
    /// Pay the unserved notice instead of working it — money the FR-HR-184 settlement will add.
    /// Mutually exclusive with <see cref="WaiveNotice"/>.
    /// </summary>
    public bool PayNoticeInLieu { get; set; }
}

/// <summary>Refuse a separation. The reason is required — a refusal nobody can explain is not a decision.</summary>
public class RejectEmployeeSeparationDto
{
    [MaxLength(1000)]
    public string Reason { get; set; } = string.Empty;
}

/// <summary>
/// Submit a draft separation into the approval queue. Carries no dates: everything it needs is
/// already on the record, and a payload that could restate them would be a second way to set the
/// facts the settlement is computed from.
/// </summary>
public class SubmitEmployeeSeparationDto
{
    [MaxLength(2000)]
    public string? Notes { get; set; }
}

/// <summary>A file attached to a separation.</summary>
public class EmployeeSeparationDocumentDto
{
    public Guid Id { get; set; }
    public Guid SeparationId { get; set; }

    public SeparationDocumentCategory Category { get; set; }
    public string CategoryName { get; set; } = string.Empty;

    public string FileName { get; set; } = string.Empty;
    public string? Description { get; set; }

    public DateTime UploadedOn { get; set; }
    public Guid? UploadedById { get; set; }
    public string? UploadedByName { get; set; }

    /// <summary>True once the file is registered in the central document repository.</summary>
    public bool IsRegisteredInDms { get; set; }

    // Deliberately absent: FilePath. The stored location is not something a client needs, and not
    // something an exit register should hand out — the file is served by the download endpoint,
    // which re-checks entitlement. Size and content type are absent too: they live on the
    // FileUploadRecord the gate created, and duplicating them here would need a migration to say
    // what a join already knows.
}

/// <summary>Filters for the exit register.</summary>
public class EmployeeSeparationQueryDto
{
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 25;

    public Guid? EmployeeId { get; set; }
    public EmployeeTerminationType? SeparationType { get; set; }
    public SeparationStatus? Status { get; set; }

    /// <summary>Restrict to separations effective on or after this date.</summary>
    public DateOnly? EffectiveFrom { get; set; }

    /// <summary>Restrict to separations effective on or before this date.</summary>
    public DateOnly? EffectiveTo { get; set; }

    /// <summary>Employee name, employee number or separation number.</summary>
    public string? Search { get; set; }

    /// <summary>
    /// When true, return only separations that have completed without the employee's master record
    /// being updated — the 29-orphan defect as a query.
    /// </summary>
    public bool? OnlyUnappliedToEmployee { get; set; }
}

// =============================================================================
// CLEARANCE — FR-HR-091 (the gate) and FR-HR-183 (what it runs across)
// =============================================================================

/// <summary>One line of the tenant's clearance form.</summary>
public class SeparationClearanceTemplateDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;

    public ClearanceItemKind Kind { get; set; }
    public string KindName { get; set; } = string.Empty;

    public string? Description { get; set; }

    public Guid? OwningOrganizationUnitId { get; set; }
    public string? OwningOrganizationUnitName { get; set; }

    public bool IsMandatory { get; set; }
    public bool IsActive { get; set; }
    public int SortOrder { get; set; }

    /// <summary>True for the kinds that can carry money — a loan, an advance, a payroll recovery.</summary>
    public bool CarriesAmount { get; set; }
}

public class CreateSeparationClearanceTemplateDto
{
    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [Required]
    public ClearanceItemKind Kind { get; set; } = ClearanceItemKind.Other;

    [MaxLength(1000)]
    public string? Description { get; set; }

    public Guid? OwningOrganizationUnitId { get; set; }
    public bool IsMandatory { get; set; } = true;
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
}

public class UpdateSeparationClearanceTemplateDto
{
    [MaxLength(200)]
    public string? Name { get; set; }

    public ClearanceItemKind? Kind { get; set; }

    [MaxLength(1000)]
    public string? Description { get; set; }

    public Guid? OwningOrganizationUnitId { get; set; }
    public bool? IsMandatory { get; set; }
    public bool? IsActive { get; set; }
    public int? SortOrder { get; set; }
}

/// <summary>One line of one employee's clearance form.</summary>
public class SeparationClearanceItemDto
{
    public Guid Id { get; set; }
    public Guid SeparationId { get; set; }
    public Guid? TemplateId { get; set; }

    public string Name { get; set; } = string.Empty;

    public ClearanceItemKind Kind { get; set; }
    public string KindName { get; set; } = string.Empty;

    public Guid? OwningOrganizationUnitId { get; set; }
    public string? OwningOrganizationUnitName { get; set; }

    public bool IsMandatory { get; set; }
    public int SortOrder { get; set; }

    public ClearanceItemStatus Status { get; set; }
    public string StatusName { get; set; } = string.Empty;

    public decimal? OutstandingAmount { get; set; }
    public bool CarriesAmount { get; set; }

    public string? Notes { get; set; }
    public string? SignedOffBy { get; set; }

    public Guid? RecordedById { get; set; }
    public string? RecordedByName { get; set; }
    public DateTime? RecordedOn { get; set; }
}

/// <summary>Record one line's answer.</summary>
public class RecordClearanceItemDto
{
    [Required]
    public ClearanceItemStatus Status { get; set; }

    /// <summary>Who in the owning unit gave the answer, as it appears on the form.</summary>
    [MaxLength(200)]
    public string? SignedOffBy { get; set; }

    /// <summary>
    /// What is still owed or unreturned. Only for the kinds that carry money, and read by the
    /// FR-HR-184 settlement.
    /// </summary>
    public decimal? OutstandingAmount { get; set; }

    [MaxLength(2000)]
    public string? Notes { get; set; }
}

/// <summary>A separation's clearance form, and whether the FR-HR-091 gate can open.</summary>
public class SeparationClearanceDto
{
    public Guid SeparationId { get; set; }
    public string SeparationNumber { get; set; } = string.Empty;
    public string EmployeeName { get; set; } = string.Empty;

    public SeparationStatus SeparationStatus { get; set; }
    public string SeparationStatusName { get; set; } = string.Empty;

    public List<SeparationClearanceItemDto> Items { get; set; } = new();

    public int TotalItems { get; set; }
    public int PendingItems { get; set; }
    public int ClearedItems { get; set; }
    public int BlockedItems { get; set; }
    public int WaivedItems { get; set; }
    public int NotApplicableItems { get; set; }

    /// <summary>Mandatory lines still without a terminal answer, or answered Blocked.</summary>
    public int MandatoryOutstanding { get; set; }

    /// <summary>
    /// Everything still owed across the form. The FR-HR-184 settlement deducts this; it is shown
    /// here so nobody has to add it up by eye before releasing someone.
    /// </summary>
    public decimal TotalOutstandingAmount { get; set; }

    /// <summary>
    /// Whether the clearance can be completed — FR-HR-091's gate, stated as an answer rather than
    /// left for the caller to infer.
    /// </summary>
    public bool CanComplete { get; set; }

    /// <summary>Why not, when <see cref="CanComplete"/> is false.</summary>
    public string? BlockedReason { get; set; }
}

// =============================================================================
// FINAL SETTLEMENT — FR-HR-184
// =============================================================================

/// <summary>One line of a final settlement.</summary>
public class SeparationSettlementLineDto
{
    public Guid Id { get; set; }
    public Guid SettlementId { get; set; }

    public SettlementLineCategory Category { get; set; }
    public string CategoryName { get; set; } = string.Empty;

    public bool IsDeduction { get; set; }
    public string Description { get; set; } = string.Empty;

    /// <summary>Null where the line could not be valued. Null is not zero.</summary>
    public decimal? Amount { get; set; }

    public SettlementLineComputation Computation { get; set; }
    public string ComputationName { get; set; } = string.Empty;

    public string? Basis { get; set; }
    public string? SourceReference { get; set; }
    public Guid? SourceClearanceItemId { get; set; }
    public Guid? SourceTravelAdvanceId { get; set; }
    public bool IsSystemGenerated { get; set; }
    public int SortOrder { get; set; }
}

/// <summary>What a leaver is owed and owes back, with the totals derived from the lines.</summary>
public class SeparationSettlementDto
{
    public Guid Id { get; set; }
    public Guid SeparationId { get; set; }
    public string SeparationNumber { get; set; } = string.Empty;
    public string EmployeeName { get; set; } = string.Empty;

    public SeparationStatus SeparationStatus { get; set; }
    public string SeparationStatusName { get; set; } = string.Empty;

    public string CurrencyCode { get; set; } = string.Empty;
    public decimal? DailyRate { get; set; }
    public string? DailyRateBasis { get; set; }

    public List<SeparationSettlementLineDto> Lines { get; set; } = new();

    /// <summary>Everything payable to the employee, across lines that could be valued.</summary>
    public decimal GrossEarnings { get; set; }

    /// <summary>Everything recoverable from them.</summary>
    public decimal TotalDeductions { get; set; }

    /// <summary>Earnings less deductions. Meaningful only when nothing is left uncomputed.</summary>
    public decimal NetPayable { get; set; }

    /// <summary>
    /// Lines the system could not value. While this is above zero the net figure is incomplete and
    /// the statement cannot be finalised.
    /// </summary>
    public int UncomputedLines { get; set; }

    public bool IsFinalised { get; set; }
    public DateTime? FinalisedOn { get; set; }
    public string? FinalisedByName { get; set; }

    public Guid? PreparedById { get; set; }
    public string? PreparedByName { get; set; }
    public DateTime? PreparedOn { get; set; }
    public string? Notes { get; set; }

    /// <summary>Whether the statement can be closed for Internal Audit's review (FR-HR-185).</summary>
    public bool CanFinalise { get; set; }

    /// <summary>Why not, when it cannot.</summary>
    public string? BlockedReason { get; set; }

    // ── FR-HR-185: Internal Audit's review ─────────────────────────────────

    public SettlementReviewOutcome ReviewOutcome { get; set; }
    public string ReviewOutcomeName { get; set; } = string.Empty;

    public Guid? ReviewedById { get; set; }
    public string? ReviewedByName { get; set; }
    public DateTime? ReviewedOn { get; set; }
    public string? ReviewNotes { get; set; }

    /// <summary>How many times Internal Audit has sent this statement back.</summary>
    public int ReturnCount { get; set; }

    /// <summary>True once Internal Audit has passed it and payment may be released.</summary>
    public bool IsClearedForPayment { get; set; }
}

/// <summary>Add a line by hand — the FR-HR-184 items the system cannot work out for itself.</summary>
public class AddSettlementLineDto
{
    [Required]
    public SettlementLineCategory Category { get; set; }

    public bool IsDeduction { get; set; }

    [Required]
    [MaxLength(300)]
    public string Description { get; set; } = string.Empty;

    /// <summary>Omit to record the line as still uncomputed.</summary>
    public decimal? Amount { get; set; }

    /// <summary>
    /// Where a hand-entered figure came from — a payroll report, a loan statement, a letter.
    /// Required whenever an amount is supplied.
    /// </summary>
    [MaxLength(300)]
    public string? SourceReference { get; set; }
}

/// <summary>Amend a line while the statement is still a draft.</summary>
public class UpdateSettlementLineDto
{
    [MaxLength(300)]
    public string? Description { get; set; }

    public decimal? Amount { get; set; }

    [MaxLength(300)]
    public string? SourceReference { get; set; }

    public bool? IsDeduction { get; set; }
}

/// <summary>Close the statement for Internal Audit's review.</summary>
public class FinaliseSettlementDto
{
    [MaxLength(2000)]
    public string? Notes { get; set; }
}

/// <summary>
/// Internal Audit's verdict on a settlement (FR-HR-185). Notes are optional on approval and
/// required on a return — a control that refuses without saying why cannot be acted on.
/// </summary>
public class ReviewSettlementDto
{
    [MaxLength(2000)]
    public string? Notes { get; set; }
}

// =============================================================================
// RETIREMENT — FR-HR-093: age 60, effective on the birthday, with advance alerts
// =============================================================================

/// <summary>One employee approaching, or past, their retirement date.</summary>
public class UpcomingRetirementDto
{
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public string? EmployeeNumber { get; set; }
    public string? PositionTitle { get; set; }
    public string? OrganizationUnitName { get; set; }

    public DateOnly? DateOfBirth { get; set; }
    public int? CurrentAge { get; set; }

    /// <summary>
    /// The retirement age applied — the tenant's compulsory age, honouring a gender-specific
    /// override where one is configured.
    /// </summary>
    public int RetirementAge { get; set; }

    /// <summary>The birthday it takes effect on (FR-HR-093).</summary>
    public DateOnly RetirementDate { get; set; }

    /// <summary>Negative once the date has passed — see <see cref="IsOverdue"/>.</summary>
    public int DaysUntilRetirement { get; set; }

    /// <summary>
    /// True where the retirement date has already passed and the employee is still on strength.
    /// Not a projection but a backlog: somebody who should already have left.
    /// </summary>
    public bool IsOverdue { get; set; }

    /// <summary>
    /// True where the explicit <c>Employee.RetirementDate</c> was used rather than a date derived
    /// from the birthday — an agreed extension, say.
    /// </summary>
    public bool IsExplicitDate { get; set; }

    /// <summary>Set where a separation has already been raised, so the sweep skips them.</summary>
    public Guid? ExistingSeparationId { get; set; }
    public string? ExistingSeparationNumber { get; set; }
    public string? ExistingSeparationStatus { get; set; }
}

/// <summary>What a retirement sweep did.</summary>
public class RetirementSweepResultDto
{
    public int HorizonDays { get; set; }

    /// <summary>Employees found due within the horizon.</summary>
    public int DueCount { get; set; }

    /// <summary>Separations the sweep raised.</summary>
    public int RaisedCount { get; set; }

    /// <summary>Already had a separation, so nothing was raised for them.</summary>
    public int SkippedExistingCount { get; set; }

    /// <summary>Could not be raised, with the reason — a missing date of birth, usually.</summary>
    public List<string> Failures { get; set; } = new();

    public List<EmployeeSeparationListDto> Raised { get; set; } = new();
}

// =============================================================================
// CONTRACT EXPIRY — FR-HR-111's other due event, and an FR-HR-182 exit route
// =============================================================================

/// <summary>One employee whose contract is running out, or has already run out.</summary>
public class UpcomingContractExpiryDto
{
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public string? EmployeeNumber { get; set; }
    public string? PositionTitle { get; set; }
    public string? OrganizationUnitName { get; set; }

    public Guid ContractId { get; set; }
    public string? ContractNumber { get; set; }
    public DateOnly? ContractStartDate { get; set; }
    public DateOnly ContractEndDate { get; set; }

    public int DaysUntilExpiry { get; set; }

    /// <summary>True where the contract has already run out and the employee is still on strength.</summary>
    public bool IsOverdue { get; set; }

    public Guid? ExistingSeparationId { get; set; }
    public string? ExistingSeparationNumber { get; set; }
    public string? ExistingSeparationStatus { get; set; }
}

/// <summary>What a contract-expiry sweep did.</summary>
public class ContractExpirySweepResultDto
{
    public int HorizonDays { get; set; }
    public int DueCount { get; set; }
    public int RaisedCount { get; set; }
    public int SkippedExistingCount { get; set; }
    public List<string> Failures { get; set; } = new();
    public List<EmployeeSeparationListDto> Raised { get; set; } = new();
}

/// <summary>
/// What the disciplinary-orphan repair found, and what it did about it.
/// </summary>
/// <remarks>
/// The defect this area was opened on: measured 2026-08-20, <b>29 disciplinary terminations whose
/// employees were all still <c>StaffStatus = Active</c></b>, because area 9 recorded the decision
/// and nothing carried it into an exit. This repair raises the separation that should have existed;
/// it does <b>not</b> terminate anybody, because each of those exits still has to go through
/// clearance, signature and settlement like any other.
/// </remarks>
public class DisciplinaryOrphanRepairDto
{
    /// <summary>True when nothing was written — the report only.</summary>
    public bool DryRun { get; set; }

    /// <summary>Disciplinary terminations with no separation against them.</summary>
    public int FoundCount { get; set; }

    /// <summary>Separations actually raised. Zero on a dry run.</summary>
    public int RaisedCount { get; set; }

    /// <summary>
    /// Found, but the employee is already off strength by some other route — nothing to repair.
    /// </summary>
    public int AlreadyTerminatedCount { get; set; }

    /// <summary>On a dry run: who would get a separation, and of what type.</summary>
    public List<string> WouldRaise { get; set; } = new();

    public List<string> Failures { get; set; } = new();

    public List<EmployeeSeparationListDto> Raised { get; set; } = new();
}
