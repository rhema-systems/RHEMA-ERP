using System.ComponentModel.DataAnnotations;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.DTOs.HR;

// ─── Leave Type ──────────────────────────────────────────────────────────────

public class LeaveTypeDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsPaid { get; set; }
    public int DefaultDaysPerYear { get; set; }
    public int MaxDaysPerYear { get; set; }
    public int? MinDaysNotice { get; set; }
    public bool RequiresApproval { get; set; }
    public string? CalendarColor { get; set; }
    public bool HasSubTypes { get; set; }
    public bool AllowCarryOver { get; set; }
    public int? MaxCarryOverDays { get; set; }
    public bool CountWeekendsAsLeave { get; set; }
    public bool CountHolidaysAsLeave { get; set; }
    public bool AllowCashConversion { get; set; }
    public bool RequiresReliever { get; set; }
    public int? MinServiceMonthsToAccess { get; set; }
    public int? CarryOverExpiryMonths { get; set; }
    public int? ForfeitUnusedAfterMonths { get; set; }
    public bool MandatoryAnnualLeave { get; set; }
    public EncashmentRateBasis EncashmentRateBasis { get; set; }
    public decimal? EncashmentRatePerDay { get; set; }
    public int EncashmentWorkingDaysPerMonth { get; set; }
    public bool IsActive { get; set; }
}

public class CreateLeaveTypeDto
{
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsPaid { get; set; } = true;
    public int DefaultDaysPerYear { get; set; }
    public int MaxDaysPerYear { get; set; }
    public int? MinDaysNotice { get; set; }
    public bool RequiresApproval { get; set; } = true;

    // UpdateLeaveTypeDto inherits this class, so one annotation covers both write paths.
    [MaxLength(9)]
    [RegularExpression(Shared.Constants.Colors.HexPattern, ErrorMessage = Shared.Constants.Colors.HexMessage)]
    public string? CalendarColor { get; set; }

    public bool HasSubTypes { get; set; }
    public bool AllowCarryOver { get; set; }
    public int? MaxCarryOverDays { get; set; }
    public bool CountWeekendsAsLeave { get; set; } = true;
    public bool CountHolidaysAsLeave { get; set; }
    public bool AllowCashConversion { get; set; }
    public bool RequiresReliever { get; set; }
    public int? MinServiceMonthsToAccess { get; set; }
    public int? CarryOverExpiryMonths { get; set; }
    public int? ForfeitUnusedAfterMonths { get; set; }
    public bool MandatoryAnnualLeave { get; set; }
    public EncashmentRateBasis EncashmentRateBasis { get; set; } = EncashmentRateBasis.DerivedFromEmoluments;
    public decimal? EncashmentRatePerDay { get; set; }
    public int EncashmentWorkingDaysPerMonth { get; set; } = 22;
    /// <summary>Allowance pay-component IDs whose value feeds this leave type's derived encashment rate.</summary>
    public List<Guid> AllowanceComponentIds { get; set; } = new();
}

public class UpdateLeaveTypeDto : CreateLeaveTypeDto
{
    public bool IsActive { get; set; } = true;
}

public class LeaveTypeDetailDto : LeaveTypeDto
{
    public List<LeaveSubTypeDto> SubTypes { get; set; } = new();
    public List<LeaveCategoryAllocationDto> Allocations { get; set; } = new();
    public List<LeaveTypeEligibilityDto> Eligibilities { get; set; } = new();
    public List<LeaveAccrualPolicyDto> AccrualPolicies { get; set; } = new();
    public List<Guid> AllowanceComponentIds { get; set; } = new();
}

// ─── Leave Sub Type ───────────────────────────────────────────────────────────

public class LeaveSubTypeDto
{
    public Guid Id { get; set; }
    public Guid LeaveTypeId { get; set; }
    public string LeaveTypeName { get; set; } = string.Empty;
    public string SubTypeName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int? MaxDaysAllowed { get; set; }
    public bool IsActive { get; set; }
}

public class CreateLeaveSubTypeDto
{
    public Guid LeaveTypeId { get; set; }
    public string SubTypeName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int? MaxDaysAllowed { get; set; }
    public bool IsActive { get; set; } = true;
}

// ─── Leave Category Allocation ────────────────────────────────────────────────

public class LeaveCategoryAllocationDto
{
    public Guid Id { get; set; }
    public Guid LeaveTypeId { get; set; }
    public string LeaveTypeName { get; set; } = string.Empty;
    public Guid? LeaveSubTypeId { get; set; }
    public string? LeaveSubTypeName { get; set; }
    public Guid StaffLevelId { get; set; }
    public string StaffLevelName { get; set; } = string.Empty;
    public int AllocationDays { get; set; }
    public DateOnly EffectiveFrom { get; set; }
    public DateOnly? EffectiveTo { get; set; }
}

public class CreateLeaveCategoryAllocationDto
{
    public Guid LeaveTypeId { get; set; }
    public Guid? LeaveSubTypeId { get; set; }
    public Guid StaffLevelId { get; set; }
    public int AllocationDays { get; set; }
    public DateOnly EffectiveFrom { get; set; }
    public DateOnly? EffectiveTo { get; set; }
}

// ─── Leave Type Eligibility ───────────────────────────────────────────────────

public class LeaveTypeEligibilityDto
{
    public Guid Id { get; set; }
    public Guid LeaveTypeId { get; set; }
    public string LeaveTypeName { get; set; } = string.Empty;
    public LeaveEligibilityType EligibilityType { get; set; }
    public Guid? OrganizationLevelId { get; set; }
    public string? OrganizationLevelName { get; set; }
    public Guid? OrganizationUnitId { get; set; }
    public string? OrganizationUnitName { get; set; }
    public Guid? PositionId { get; set; }
    public string? PositionName { get; set; }
    public Gender? Gender { get; set; }
}

public class CreateLeaveTypeEligibilityDto
{
    public Guid LeaveTypeId { get; set; }
    public LeaveEligibilityType EligibilityType { get; set; }
    public Guid? OrganizationLevelId { get; set; }
    public Guid? OrganizationUnitId { get; set; }
    public Guid? PositionId { get; set; }
    public Gender? Gender { get; set; }
}

// ─── Leave Accrual Policy ─────────────────────────────────────────────────────

public class LeaveAccrualPolicyDto
{
    public Guid Id { get; set; }
    public Guid LeaveTypeId { get; set; }
    public string LeaveTypeName { get; set; } = string.Empty;
    public AccrualFrequency Frequency { get; set; }
    public AccrualMode Mode { get; set; }
    public decimal AccrualRate { get; set; }
    public int? MinServiceMonths { get; set; }
    public bool ProRateOnJoin { get; set; }
    public bool ProRateOnExit { get; set; }
    public bool IsActive { get; set; }
}

public class CreateLeaveAccrualPolicyDto
{
    public Guid LeaveTypeId { get; set; }
    public AccrualFrequency Frequency { get; set; } = AccrualFrequency.Monthly;
    public AccrualMode Mode { get; set; } = AccrualMode.AccrueIncrementally;
    public decimal AccrualRate { get; set; }
    public int? MinServiceMonths { get; set; }
    public bool ProRateOnJoin { get; set; }
    public bool ProRateOnExit { get; set; }
}

// ─── Leave Balance ────────────────────────────────────────────────────────────

public class LeaveBalanceDto
{
    public Guid Id { get; set; }
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public string? OrganizationUnitName { get; set; }
    public Guid LeaveTypeId { get; set; }
    public string LeaveTypeName { get; set; } = string.Empty;
    public Guid? LeaveSubTypeId { get; set; }
    public string? LeaveSubTypeName { get; set; }
    public int Year { get; set; }
    public decimal EntitledDays { get; set; }
    public decimal AccruedToDateDays { get; set; }
    public decimal UsedDays { get; set; }
    public decimal PendingDays { get; set; }
    public decimal CarriedOverDays { get; set; }
    public decimal AdjustmentDays { get; set; }
    public decimal EncashedDays { get; set; }
    /// <summary>The policy figure: <c>Entitled + Carried + Adjustments − Used − Pending − Encashed</c>.</summary>
    public decimal AvailableDays { get; set; }
    /// <summary>
    /// The figure the create check actually enforces — the same sum with <see cref="AccruedToDateDays"/>
    /// in place of <see cref="EntitledDays"/> for leave types that accrue. Mid-year the two differ by
    /// whatever has not accrued yet, and a screen showing only the first invites a refused request.
    /// </summary>
    public decimal AccruedAvailableDays { get; set; }
}

// ─── Leave Adjustment ────────────────────────────────────────────────────────

public class LeaveAdjustmentDto
{
    public Guid     Id               { get; set; }
    public Guid     LeaveBalanceId   { get; set; }
    public Guid     EmployeeId       { get; set; }
    public string   EmployeeName     { get; set; } = string.Empty;
    public Guid     LeaveTypeId      { get; set; }
    public string   LeaveTypeName    { get; set; } = string.Empty;
    public Guid?    LeaveSubTypeId   { get; set; }
    public string?  LeaveSubTypeName { get; set; }
    public int      Year             { get; set; }
    public decimal  Days             { get; set; }
    public Guid?    ReasonCodeId     { get; set; }
    public string?  ReasonCodeName   { get; set; }
    public string   Reason           { get; set; } = string.Empty; // free-text "Remarks"
    public DateTime AdjustmentDate   { get; set; }
    public Guid     PerformedBy      { get; set; }
    public string   PerformedByName  { get; set; } = string.Empty;
}

/// <summary>
/// Add an adjustment against a known balance.
/// </summary>
/// <remarks>
/// ⚠ Finish-plan lane 4 (2026-09-01): <c>PerformedBy</c> is no longer accepted from the caller. It is
/// an Employee foreign key, and the desk screen was sending the LOGIN's user id — a value that is
/// never an employee id — so every adjustment raised from the screen failed on the constraint. The
/// service now stamps the acting employee from the token; a caller-supplied value would have been
/// forgeable in any case.
/// </remarks>
public class CreateLeaveAdjustmentDto
{
    public Guid     LeaveBalanceId { get; set; }
    public decimal  Days           { get; set; }
    public Guid?    ReasonCodeId   { get; set; }
    /// <summary>Free-text remarks. Labelled "Remarks" in the UI, beside the reason code.</summary>
    public string   Reason         { get; set; } = string.Empty;
}

/// <summary>Create a leave adjustment from scratch — no balance ID needed.</summary>
public class CreateLeaveAdjustmentStandaloneDto
{
    public Guid      EmployeeId     { get; set; }
    public Guid      LeaveTypeId    { get; set; }
    public Guid?     LeaveSubTypeId { get; set; }
    public int       Year           { get; set; }
    public decimal   Days           { get; set; }
    public Guid?     ReasonCodeId   { get; set; }
    /// <summary>Free-text remarks. Labelled "Remarks" in the UI, beside the reason code.</summary>
    public string    Reason         { get; set; } = string.Empty;
    // PerformedBy is stamped from the token — see CreateLeaveAdjustmentDto.
    public DateTime? AdjustmentDate { get; set; }
}

/// <summary>Update the mutable fields of an existing adjustment.</summary>
public class UpdateLeaveAdjustmentDto
{
    public decimal   Days           { get; set; }
    public Guid?     ReasonCodeId   { get; set; }
    public string    Reason         { get; set; } = string.Empty;
    public DateTime? AdjustmentDate { get; set; }
}

// ─── Leave Balance Detail (drill-down) ───────────────────────────────────────

public class LeaveBalanceDetailDto
{
    public Guid    Id                   { get; set; }
    public Guid    EmployeeId           { get; set; }
    public string  EmployeeName         { get; set; } = string.Empty;
    public string? OrganizationUnitName { get; set; }
    public Guid    LeaveTypeId          { get; set; }
    public string  LeaveTypeName        { get; set; } = string.Empty;
    public Guid?   LeaveSubTypeId       { get; set; }
    public string? LeaveSubTypeName     { get; set; }
    public int     Year                 { get; set; }
    public decimal EntitledDays         { get; set; }
    public decimal AccruedToDateDays    { get; set; }
    public decimal UsedDays             { get; set; }
    public decimal PendingDays          { get; set; }
    public decimal CarriedOverDays      { get; set; }
    public decimal AdjustmentDays       { get; set; }
    public decimal EncashedDays         { get; set; }
    /// <summary>
    /// The policy figure: <c>Entitled + Carried + Adjustments − Used − Pending − Encashed</c>.
    /// </summary>
    public decimal AvailableDays        { get; set; }
    /// <summary>
    /// The figure the create check actually enforces: the same sum with <see cref="AccruedToDateDays"/>
    /// in place of <see cref="EntitledDays"/> for leave types that accrue. On annual leave mid-year the
    /// two differ by whatever has not accrued yet, and a screen that shows only the first one invites a
    /// request the server will refuse (closure plan L-14).
    /// </summary>
    public decimal AccruedAvailableDays { get; set; }
    public bool    AllowCashConversion  { get; set; }
    public List<LeaveRequestDto>    Requests    { get; set; } = new();
    public List<LeaveEncashmentDto> Encashments { get; set; } = new();
    public List<LeaveAdjustmentDto> Adjustments { get; set; } = new();
}

// ─── Leave Plan ───────────────────────────────────────────────────────────────

public class LeavePlanDto
{
    public Guid Id { get; set; }
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public Guid? OrganizationLevelId { get; set; }
    public string? OrganizationLevelName { get; set; }
    public Guid? OrganizationUnitId { get; set; }
    public string? OrganizationUnitName { get; set; }
    public Guid? PositionId { get; set; }
    public string? PositionName { get; set; }
    public Guid LeaveTypeId { get; set; }
    public string LeaveTypeName { get; set; } = string.Empty;
    public Guid? LeaveSubTypeId { get; set; }
    public string? LeaveSubTypeName { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public Guid? RelieverId { get; set; }
    public string? RelieverName { get; set; }
    public Guid? SecondRelieverId { get; set; }
    public string? SecondRelieverName { get; set; }
    public string? Notes { get; set; }
    public Guid PlannedBy { get; set; }
    public string PlannedByName { get; set; } = string.Empty;
    public int Year { get; set; }
    public LeavePlanStatus Status { get; set; }

    // Manager-suggested alternative dates (ChangesSuggested)
    public DateOnly? SuggestedStartDate { get; set; }
    public DateOnly? SuggestedEndDate { get; set; }
    public string? ManagerSuggestionNotes { get; set; }

    // Workflow fields
    public Guid? WorkflowInstanceId { get; set; }
    public Guid? ApprovedById { get; set; }
    public DateTime? ApprovedDate { get; set; }
    public string? RejectionReason { get; set; }

    /// <summary>
    /// The live leave request raised from this plan, if one has been. `LeaveRequest.LeavePlanId` has
    /// existed since the port and nothing ever wrote it, so an approved plan dead-ended and the
    /// employee re-keyed their own dates (closure plan L-9 / R-1). A cancelled or rejected request
    /// does not count — the plan becomes raiseable again.
    /// </summary>
    public Guid? RaisedLeaveRequestId { get; set; }
    public string? RaisedLeaveRequestNumber { get; set; }

    /// <summary>
    /// Why the named reliever(s) may not actually be available over this plan's dates. Empty when
    /// nothing overlaps, or when no reliever is named. Advisory — a plan with clashes can still be
    /// saved and approved; what the register must never do is stay silent about them.
    /// </summary>
    /// <remarks>
    /// Finish-plan lane 4 (2026-09-01). TDC's demo feedback: "reliever clashes are not visible on
    /// the plan". Filled by the service from three sources — the reliever's own leave plans, the
    /// reliever's own leave requests, and other plans in the same window that name the same
    /// reliever — so a reliever who is away, or already covering for somebody else, shows up.
    /// </remarks>
    public List<LeaveRelieverClashDto> RelieverClashes { get; set; } = new();
}

/// <summary>One reason a reliever is not free over a leave plan's dates.</summary>
public class LeaveRelieverClashDto
{
    public Guid RelieverId { get; set; }
    public string RelieverName { get; set; } = string.Empty;
    /// <summary>Which slot the clashing reliever holds on the plan being described: 1 or 2.</summary>
    public int Slot { get; set; }
    /// <summary><c>LeavePlan</c>, <c>LeaveRequest</c> or <c>RelieverOnAnotherPlan</c>.</summary>
    public string Source { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateOnly FromDate { get; set; }
    public DateOnly ToDate { get; set; }
}

public class CreateLeavePlanDto
{
    public Guid EmployeeId { get; set; }
    public Guid? OrganizationLevelId { get; set; }
    public Guid? OrganizationUnitId { get; set; }
    public Guid? PositionId { get; set; }
    public Guid LeaveTypeId { get; set; }
    public Guid? LeaveSubTypeId { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public Guid? RelieverId { get; set; }
    public Guid? SecondRelieverId { get; set; }
    public string? Notes { get; set; }
    // PlannedBy is an Employee foreign key stamped from the token (finish-plan lane 4). Both screens
    // used to send the LOGIN's user id here, which is never an employee id. On update the original
    // planner is kept.
    //
    // Year is NOT here: it is derived from StartDate. The desk screen used to send the list filter's
    // year, so a January plan raised from the December list was filed under the wrong year and then
    // vanished from both (closure plan L-17). A plan's year is a property of its dates.
}

/// <summary>
/// Manager action on a submitted leave plan: propose alternative dates and send the
/// plan back to the employee (status becomes <c>ChangesSuggested</c>).
/// </summary>
public class SuggestLeavePlanChangesDto
{
    public DateOnly SuggestedStartDate { get; set; }
    public DateOnly SuggestedEndDate { get; set; }
    public string? Notes { get; set; }
}

/// <summary>
/// Employee response to a manager's suggested changes. <see cref="Accept"/> = true adopts
/// the manager's suggested dates; otherwise the employee counters with their own
/// <see cref="StartDate"/>/<see cref="EndDate"/>. Either way the plan is re-submitted.
/// </summary>
public class RespondToLeaveSuggestionDto
{
    public bool Accept { get; set; }
    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public string? Notes { get; set; }
}

// ─── Leave Request ────────────────────────────────────────────────────────────

/// <summary>
/// An approver sending a request back with dates of their own (status becomes
/// <c>ChangesSuggested</c>). The mirror of <see cref="SuggestLeavePlanChangesDto"/>.
/// The employee answers with <see cref="RespondToLeaveSuggestionDto"/>, which both sides share.
/// </summary>
public class SuggestLeaveRequestChangesDto
{
    public DateOnly SuggestedStartDate { get; set; }
    public DateOnly SuggestedEndDate { get; set; }
    public string? Notes { get; set; }
}

/// <summary>
/// Moving an already-approved request to different dates, keeping its number and its history.
/// </summary>
/// <remarks>
/// A reason is REQUIRED. The whole point of this path over cancel-and-re-key is that the record
/// says why it moved; an unexplained reschedule is the thing it exists to prevent.
/// </remarks>
public class RescheduleLeaveRequestDto
{
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public string Reason { get; set; } = string.Empty;
}

/// <summary>
/// Calls an employee back before their leave ends (residue plan R-14).
/// </summary>
/// <remarks>
/// ⚠ <see cref="EffectiveDate"/> is <b>the first day the employee is expected back at work</b>, not
/// the last day of their leave. That is the date a recall notice actually states, and naming it the
/// other way round is a one-day error nobody would catch on a screen — the service takes the day
/// before it as the new end date.
/// <para>A reason is REQUIRED: a recall is the employer's act, and its record has to say why.</para>
/// </remarks>
public class RecallLeaveRequestDto
{
    public DateOnly EffectiveDate { get; set; }
    public string Reason { get; set; } = string.Empty;
}

public class CreateLeaveRequestDto
{
    public Guid EmployeeId { get; set; }
    public Guid LeaveTypeId { get; set; }
    public Guid? LeaveSubTypeId { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public string Reason { get; set; } = string.Empty;
    public Guid? RelieverEmployeeId { get; set; }
    public Guid? SecondRelieverEmployeeId { get; set; }
    public string? RelieverNotes { get; set; }
    public string? HandoverNotes { get; set; }
    public Guid? LeavePlanId { get; set; }
    public bool SaveAsDraft { get; set; }
}

public class LeaveRequestDto
{
    public Guid Id { get; set; }
    public string RequestNumber { get; set; } = string.Empty;

    public Guid EmployeeId { get; set; }
    public string EmployeeNumber { get; set; } = string.Empty;
    public string EmployeeName { get; set; } = string.Empty;

    public Guid LeaveTypeId { get; set; }
    public string LeaveTypeName { get; set; } = string.Empty;
    public bool IsPaidLeave { get; set; }

    public Guid? LeaveSubTypeId { get; set; }
    public string? LeaveSubTypeName { get; set; }

    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public decimal TotalDays { get; set; }

    public DateTime RequestDate { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string? HandoverNotes { get; set; }
    public LeaveStatus Status { get; set; }

    public Guid? RelieverEmployeeId { get; set; }
    public string? RelieverEmployeeName { get; set; }
    public Guid? SecondRelieverEmployeeId { get; set; }
    public string? SecondRelieverEmployeeName { get; set; }
    public string? RelieverNotes { get; set; }

    public Guid? LeavePlanId { get; set; }

    /// <summary>The number of the plan this request was raised from, when it came from one.</summary>
    public string? LeavePlanReference { get; set; }

    /// <summary>
    /// Who finally approved the request and when, and why it was refused if it was.
    /// </summary>
    /// <remarks>
    /// ⚠ These three were on the ENTITY and on no DTO, so nothing could show who approved a piece
    /// of leave or when — the one fact an approval exists to record. Found by the hr-leave suite,
    /// which asserted an approval date after a successful approval and got nothing.
    ///
    /// On a two-stage ladder these carry the FINAL approval. The per-stage history is the workflow
    /// engine's, and is read from the request's Workflow tab.
    /// </remarks>
    public Guid? ApprovedById { get; set; }
    public DateTime? ApprovedDate { get; set; }
    public string? RejectionReason { get; set; }

    // Dates the approver sent back instead (status ChangesSuggested).
    public DateOnly? SuggestedStartDate { get; set; }
    public DateOnly? SuggestedEndDate { get; set; }
    public string? ManagerSuggestionNotes { get; set; }

    // Set once an approved request has been moved: what it used to say, who moved it, and why.
    public DateOnly? OriginalStartDate { get; set; }
    public DateOnly? OriginalEndDate { get; set; }
    public DateTime? RescheduledDate { get; set; }
    public Guid? RescheduledById { get; set; }
    public string? RescheduledByName { get; set; }
    public string? RescheduleReason { get; set; }
    public int RescheduleCount { get; set; }

    // Set when somebody answered "yes, this is still going ahead".
    public DateTime? ObservanceConfirmedDate { get; set; }
    public Guid? ObservanceConfirmedById { get; set; }
    public string? ObservanceConfirmedByName { get; set; }

    // Set when the employee was called back before their end date (residue plan R-14). The request
    // keeps its number, its status and its approval — EndDate is simply earlier than it was, and
    // PreRecallEndDate is what it used to be. A screen showing a recalled request should say both,
    // or the leave looks like it was always this short.
    public DateOnly? RecallEffectiveDate { get; set; }
    public DateOnly? PreRecallEndDate { get; set; }
    public DateTime? RecalledDate { get; set; }
    public Guid? RecalledById { get; set; }
    public string? RecalledByName { get; set; }
    public string? RecallReason { get; set; }
    public decimal? DaysRestored { get; set; }

    /// <summary>
    /// Attendance days recorded as <c>OnLeave</c> against this request. Filled on the single-request
    /// read only. It should equal <see cref="TotalDays"/> once the request is approved; a smaller
    /// number means some of those days already had attendance recorded and were left alone, and
    /// those days will not reach the payroll export as leave (closure plan L-27).
    /// </summary>
    public int AttendanceDaysRecorded { get; set; }

    public DateTime? ClosureDate { get; set; }
    public string? ClosureNotes { get; set; }
    public DateTime? CancellationDate { get; set; }
    public string? CancellationReason { get; set; }

    public DateTime CreatedAt { get; set; }
}

public class ApproveLeaveDto
{
    public Guid ApprovedBy { get; set; }
    public string? ApprovalNotes { get; set; }
    public string? Comments { get; set; }
}

/// <summary>
/// One row of the mandatory-leave compliance report: for a leave type flagged
/// <c>MandatoryAnnualLeave</c>, how much of an employee's entitlement they have actually taken
/// (used), have scheduled (pending), and still owe within the year.
/// </summary>
public class MandatoryLeaveComplianceDto
{
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public Guid LeaveTypeId { get; set; }
    public string LeaveTypeName { get; set; } = string.Empty;
    public int Year { get; set; }
    public decimal EntitledDays { get; set; }
    public decimal TakenDays { get; set; }
    public decimal ScheduledDays { get; set; }
    public decimal OutstandingDays { get; set; }
    /// <summary>"Compliant" (taken ≥ entitled), "Scheduled" (pending covers the rest), or "Outstanding".</summary>
    public string Status { get; set; } = string.Empty;
}

public class RejectLeaveDto
{
    public string RejectionReason { get; set; } = string.Empty;
}

public class CancelLeaveDto
{
    public string CancellationReason { get; set; } = string.Empty;
}

public class CloseLeaveDto
{
    public string? ClosureNotes { get; set; }
}

// ─── Leave Request Attachment ─────────────────────────────────────────────────

public class LeaveRequestAttachmentDto
{
    public Guid Id { get; set; }
    public Guid LeaveRequestId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string? ContentType { get; set; }
    public long? FileSizeBytes { get; set; }
    public DateTime UploadedDate { get; set; }
    public Guid UploadedBy { get; set; }
    public string UploadedByName { get; set; } = string.Empty;
}

// ─── Leave Encashment ─────────────────────────────────────────────────────────

public class LeaveEncashmentDto
{
    public Guid Id { get; set; }
    public Guid LeaveRequestId { get; set; }
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public Guid LeaveTypeId { get; set; }
    public string LeaveTypeName { get; set; } = string.Empty;
    public int Year { get; set; }
    public decimal DaysEncashed { get; set; }
    public decimal AmountPaid { get; set; }
    public LeaveEncashmentStatus Status { get; set; }
    public DateTime? ProcessedDate { get; set; }
    public Guid? ProcessedByEmployeeId { get; set; }
    public string? ProcessedByName { get; set; }
    public string? PaymentReference { get; set; }
    public string? Notes { get; set; }

    // Workflow fields
    public Guid? WorkflowInstanceId { get; set; }
    public Guid? ApprovedById { get; set; }
    public DateTime? ApprovedDate { get; set; }
    public string? RejectionReason { get; set; }
}

public class CreateLeaveEncashmentDto
{
    public Guid LeaveRequestId { get; set; }
    public Guid EmployeeId { get; set; }
    public Guid LeaveTypeId { get; set; }
    public int Year { get; set; }
    public decimal DaysEncashed { get; set; }
    public decimal AmountPaid { get; set; }
    public string? Notes { get; set; }
}

/// <summary>
/// Marking an encashment paid. The actor is NOT taken from the body: <c>ProcessedByEmployeeId</c>
/// is an <c>Employees</c> foreign key and is stamped from the caller's own employee id, the same
/// house rule that adjustments and plans follow.
/// </summary>
public class ProcessLeaveEncashmentDto
{
    public string PaymentReference { get; set; } = string.Empty;
}

// ─── Leave Balance Recalculation ──────────────────────────────────────────────

/// <summary>Request body for the admin recalculate endpoint.</summary>
public class RecalculateLeaveBalanceRequest
{
    public Guid  EmployeeId  { get; set; }
    public int   Year        { get; set; } = DateTime.UtcNow.Year;
    /// <summary>When null, all leave types for the employee are recalculated.</summary>
    public Guid? LeaveTypeId { get; set; }
}

// ─── Leave reminder engine (closure plan wave E, slice E2) ───────────────────

public class LeaveReminderRunResultDto
{
    public Guid RunId { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string Trigger { get; set; } = string.Empty;
    public int RemindersQueued { get; set; }

    /// <summary>How many candidates were found but already claimed by an earlier sweep.</summary>
    public int AlreadySent { get; set; }
}

/// <summary>One thing a sweep would chase, whether or not it has been chased already.</summary>
public class LeaveReminderPreviewItemDto
{
    public string Kind { get; set; } = string.Empty;
    public string ItemType { get; set; } = string.Empty;
    public Guid EntityId { get; set; }
    public Guid? EmployeeId { get; set; }
    public string? EmployeeName { get; set; }
    public string Reference { get; set; } = string.Empty;
    public DateTime? DueDate { get; set; }
    public int DaysRemaining { get; set; }
    public int EscalationTier { get; set; }
    public string DedupeKey { get; set; } = string.Empty;
    public bool AlreadySent { get; set; }
}

public class LeaveReminderRunDto
{
    public Guid Id { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string Trigger { get; set; } = string.Empty;
    public int RemindersQueued { get; set; }
}

public class LeaveReminderLogEntryDto
{
    public Guid Id { get; set; }
    public string Kind { get; set; } = string.Empty;
    public string ItemType { get; set; } = string.Empty;
    public Guid EntityId { get; set; }
    public Guid? EmployeeId { get; set; }
    public string Reference { get; set; } = string.Empty;
    public DateTime? DueDate { get; set; }
    public int DaysRemaining { get; set; }
    public int EscalationTier { get; set; }
    public DateTime SentAt { get; set; }
}

// ─── Leave calendar (closure plan wave E, slice E1) ──────────────────────────

/// <summary>Whose leave a calendar read is about.</summary>
/// <remarks>
/// Decision D-9: one component, three entry points. The scope is what makes them different, and it
/// is authorized differently for each — <c>Mine</c> needs nothing but a linked employee record,
/// <c>Team</c> is the caller's own direct reports, and <c>Organisation</c> is the leave read tier.
/// </remarks>
public enum LeaveCalendarScope
{
    Mine = 0,
    Team = 1,
    Organisation = 2
}

/// <summary>One person's leave, as a band on a calendar.</summary>
/// <remarks>
/// ⚠ It carries no reason and no balance — a calendar is read by colleagues, and "Ama is on annual
/// leave" is what a calendar is for. The same rule the reminder engine's templates follow.
/// </remarks>
public class LeaveCalendarEntryDto
{
    public Guid Id { get; set; }
    public string RequestNumber { get; set; } = string.Empty;
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public string? OrganizationUnitName { get; set; }
    public Guid LeaveTypeId { get; set; }
    public string LeaveTypeName { get; set; } = string.Empty;

    /// <summary>
    /// The leave type's own <c>CalendarColor</c>. Null when the tenant has not set one, and the
    /// screen falls back to a generated palette rather than showing nothing.
    /// </summary>
    public string? CalendarColor { get; set; }

    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public decimal TotalDays { get; set; }
    public LeaveStatus Status { get; set; }

    /// <summary>True when the leave is approved; a pending band is drawn as an outline.</summary>
    public bool IsConfirmed { get; set; }
}

/// <summary>A non-working day drawn underneath the leave bands.</summary>
public class LeaveCalendarHolidayDto
{
    public DateOnly Date { get; set; }
    public string Name { get; set; } = string.Empty;
}

public class LeaveCalendarDto
{
    public DateOnly From { get; set; }
    public DateOnly To { get; set; }
    public LeaveCalendarScope Scope { get; set; }
    public List<LeaveCalendarEntryDto> Entries { get; set; } = new();
    public List<LeaveCalendarHolidayDto> Holidays { get; set; } = new();
}

// ─── Leave register (closure plan wave E, slice E3) ──────────────────────────

/// <summary>
/// Filters for the organisation-wide leave register.
/// </summary>
/// <remarks>
/// Until this existed the only way to list leave requests was one employee at a time
/// (<c>GET employee/{id}/history</c>), so "who is off in December" or "every rejected request this
/// quarter" could not be asked at all (closure plan L-6). Every filter is optional; with none, the
/// register is the whole tenant for the date range.
/// </remarks>
public class LeaveRegisterFilterDto
{
    /// <summary>Requests overlapping this window. Both ends optional.</summary>
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }

    public LeaveStatus? Status { get; set; }
    public Guid? LeaveTypeId { get; set; }
    public Guid? EmployeeId { get; set; }
    public Guid? OrganizationUnitId { get; set; }

    /// <summary>Matches a request number or an employee's name.</summary>
    public string? Search { get; set; }
}

// ─── Bulk decisions on the leave queue (closure plan wave E, slice E4) ───────

/// <summary>
/// Approve or reject several leave requests in one go.
/// </summary>
/// <remarks>
/// A December approval queue is 200 rows, and deciding them one page-load at a time is what the
/// UAT plan recorded as missing (closure plan R-12).
/// </remarks>
public class BulkLeaveDecisionDto
{
    public List<Guid> LeaveRequestIds { get; set; } = new();

    /// <summary>Applied to every item — the shared-value shape from the bulk catalogue §4.3.</summary>
    public string? Comments { get; set; }

    /// <summary>Required when rejecting; ignored when approving.</summary>
    public string? RejectionReason { get; set; }
}

/// <summary>
/// The house bulk-result shape (bulk catalogue §4.2): a per-item reason, because the three items
/// that failed are exactly what somebody deciding fifty needs to see.
/// </summary>
public class HrBulkActionResultDto
{
    public int RequestedCount { get; set; }
    public int SucceededCount { get; set; }
    public List<HrBulkActionItemResult> Results { get; set; } = new();
}

public class HrBulkActionItemResult
{
    public Guid Id { get; set; }
    public bool Success { get; set; }

    /// <summary>Why it did not go through. Null on success.</summary>
    public string? Reason { get; set; }
}
