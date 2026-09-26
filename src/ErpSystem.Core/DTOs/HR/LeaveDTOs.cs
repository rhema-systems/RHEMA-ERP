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

    /// <summary>⚠ Governs BOTH year-end acts — carry-over and forfeiture. Default <c>Granted</c>.</summary>
    public LeaveYearEndBasis YearEndBasis { get; set; }

    /// <summary>⚠ Refused together with an incremental accrual policy — the two deduct for the same months.</summary>
    public bool ProRateFirstYearEntitlement { get; set; }

    /// <summary>Annual, Maternity or Other (round 5, A4). Replaces <c>MandatoryAnnualLeave</c>.</summary>
    public LeaveTypeCategory Category { get; set; }

    /// <summary>
    /// Days asked for beyond this leave's limit may be charged to annual leave, HR deciding at the
    /// final approval (round 5, decision A5). Other kinds only.
    /// </summary>
    public bool AllowOffsetAgainstAnnual { get; set; }
    public EncashmentRateBasis EncashmentRateBasis { get; set; }
    public decimal? EncashmentRatePerDay { get; set; }
    public int EncashmentWorkingDaysPerMonth { get; set; }

    /// <summary>Whether this leave type requires excuse duty (a medical certificate).</summary>
    public bool RequiresMedicalCertificate { get; set; }
    /// <summary>Days takeable on the employee's own word before a certificate is required.</summary>
    public int SelfCertificationDays { get; set; }
    /// <summary>Cumulative days in a year past which a medical board must sit. Null = never.</summary>
    public int? MedicalBoardThresholdDays { get; set; }
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

    /// <summary>⚠ Governs BOTH year-end acts — carry-over and forfeiture. Default <c>Granted</c>.</summary>
    public LeaveYearEndBasis YearEndBasis { get; set; }

    /// <summary>⚠ Refused together with an incremental accrual policy — the two deduct for the same months.</summary>
    public bool ProRateFirstYearEntitlement { get; set; }

    /// <summary>
    /// Annual, Maternity or Other (round 5, A4). ⚠ <b>Null means "not saying"</b>: Other on create,
    /// and UNCHANGED on update.
    /// </summary>
    /// <remarks>
    /// Nullable on purpose. The update is a whole-object PUT, and callers that read a leave type and
    /// echo it back (the demo builder among them) predate the kind. A non-nullable field would quietly
    /// turn the tenant's Annual Leave into Other on their next save: the L-13 shape, where an
    /// unmentioned field means "clear it".
    /// </remarks>
    public LeaveTypeCategory? Category { get; set; }

    /// <summary>
    /// Days beyond the limit may be charged to annual leave (round 5, A5). ⚠ <b>Null means "not
    /// saying"</b>, as for <see cref="Category"/>: off on create, UNCHANGED on update. Refused on an
    /// Annual or Maternity type, and on a type that does not require approval.
    /// </summary>
    public bool? AllowOffsetAgainstAnnual { get; set; }
    public EncashmentRateBasis EncashmentRateBasis { get; set; } = EncashmentRateBasis.DerivedFromEmoluments;
    public decimal? EncashmentRatePerDay { get; set; }
    public int EncashmentWorkingDaysPerMonth { get; set; } = 22;

    /// <summary>
    /// Whether this leave type requires excuse duty — a medical certificate — once the
    /// self-certification period is passed. Defaults off, so no existing type starts refusing.
    /// </summary>
    public bool RequiresMedicalCertificate { get; set; } = false;

    /// <summary>Days takeable on the employee's own word. ⚠ 3 is a starting value, not a rule.</summary>
    [Range(0, 365)] public int SelfCertificationDays { get; set; } = 3;

    /// <summary>
    /// Cumulative days of this type in a year past which a medical board must sit. Null = never.
    /// ⚠ Counted across the YEAR: a per-request threshold is defeated by splitting an absence.
    /// </summary>
    [Range(1, 365)] public int? MedicalBoardThresholdDays { get; set; } = 90;

    /// <summary>Allowance pay-component IDs whose value feeds this leave type's derived encashment rate.</summary>
    public List<Guid> AllowanceComponentIds { get; set; } = new();
}

/// <summary>
/// Updating a leave type. ⚠ <b>A PUT here is a REPLACE</b>, as the verb says: every field sent
/// becomes the new value and every field omitted becomes its default.
/// </summary>
/// <remarks>
/// <para>⚠ <b>That is dangerous for the allowance links, and finding L-13 is about exactly this.</b>
/// <c>LeaveTypeAllowance</c> rows decide what a day of encashed leave is <b>worth</b> — they feed
/// the derived rate — and a caller who sent a partial body used to have every one of them silently
/// deleted, changing people's money with no trace of what was removed.</para>
///
/// <para><b>So this one property is nullable and the others are not.</b> <c>null</c> means "I am not
/// touching the allowances" and an empty list means "remove them all". A PUT that omits the field
/// now leaves the links alone; a caller who genuinely wants none sends <c>[]</c> and says so. The
/// replace-set semantics stay for every other field, because that is what PUT means and the screens
/// send the whole object — but a flag reset to false is visible on the next read, whereas a deleted
/// link is not.</para>
/// </remarks>
public class UpdateLeaveTypeDto : CreateLeaveTypeDto
{
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// ⚠ <c>null</c> = leave the allowance links untouched. <c>[]</c> = remove them all.
    /// Shadows the non-nullable property on the create DTO, deliberately.
    /// </summary>
    public new List<Guid>? AllowanceComponentIds { get; set; }
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

    /// <summary>
    /// Whether the policy is in force (round 5, lane N1). It existed on the entity with nothing to
    /// write it, so a policy could only be deleted. <c>null</c> means active on create and
    /// unchanged on update, so an older caller cannot switch a policy off by leaving it out.
    /// </summary>
    public bool? IsActive { get; set; }
}

// ─── Leave Balance ────────────────────────────────────────────────────────────

public class LeaveBalanceDto
{
    public Guid Id { get; set; }

    /// <summary>
    /// False on a figure worked out live for somebody with no balance record for the year yet (round
    /// 5, lane J): the annual view and the portal show everybody's annual leave before a request has
    /// opened a record. Such a row has no <see cref="Id"/>, so there is nothing to open, adjust or
    /// recalculate; its figures are exactly what the record will hold when it is created.
    /// </summary>
    public bool HasRecord { get; set; } = true;
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;

    /// <summary>The staff number, for finding a person in a long list.</summary>
    public string? EmployeeNumber { get; set; }
    public string? OrganizationUnitName { get; set; }
    public Guid LeaveTypeId { get; set; }
    public string LeaveTypeName { get; set; } = string.Empty;

    /// <summary>The leave type's kind (round 5, A4): Annual shows as a balance, Other as a limit.</summary>
    public LeaveTypeCategory? LeaveTypeCategory { get; set; }
    public Guid? LeaveSubTypeId { get; set; }
    public string? LeaveSubTypeName { get; set; }
    public int Year { get; set; }
    public decimal EntitledDays { get; set; }
    public decimal AccruedToDateDays { get; set; }

    /// <summary>
    /// The date <see cref="AccruedToDateDays"/> is worked out to (round 5, lane C2) — today, the year
    /// end, or a leaver's last day. <c>null</c> for a leave type that does not accrue.
    /// </summary>
    public DateOnly? AccruedAsOf { get; set; }
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

    /// <summary>
    /// While the employee has not yet served the leave type's qualifying period: the first day they
    /// may take it (round 5, lane J). Null once they may, and for a type without one.
    /// </summary>
    public DateOnly? AccessibleFrom { get; set; }
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
    /// <summary>The date <see cref="AccruedToDateDays"/> is worked out to (round 5, lane C2).</summary>
    public DateOnly? AccruedAsOf        { get; set; }
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

// ─── Accrual statement (round 5, lane C2) ────────────────────────────────────

/// <summary>
/// How one balance's accrual is worked out as at a date: the rule, the entitlement and where it came
/// from, the rate, and one line per completed period with a running total.
/// </summary>
/// <remarks>
/// The answer to "HR might run a utility that accrues the leave days up to a date": nothing needs
/// running, because accrual is worked out whenever it is asked for — this shows the working, for
/// any date. Every figure comes from the same method the create check and the balance screens use.
/// </remarks>
public class LeaveAccrualStatementDto
{
    public Guid BalanceId { get; set; }
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public Guid LeaveTypeId { get; set; }
    public string LeaveTypeName { get; set; } = string.Empty;
    public LeaveTypeCategory? LeaveTypeCategory { get; set; }
    public int Year { get; set; }
    public DateOnly YearStart { get; set; }
    public DateOnly YearEnd { get; set; }

    /// <summary>The date asked for (today when none was given).</summary>
    public DateOnly RequestedAsOf { get; set; }

    /// <summary>The date it is worked out to: earlier than asked at the year end or a leaver's last day.</summary>
    public DateOnly AsOf { get; set; }
    public LeaveAccrualAsOfLimit AsOfLimit { get; set; }
    public LeaveAccrualState State { get; set; }

    // The entitlement — the cap, and for a derived rate its source.
    public decimal AnnualEntitledDays { get; set; }
    public LeaveEntitlementSource EntitlementSource { get; set; }
    public decimal EntitlementBaseDays { get; set; }
    public string? StaffLevelName { get; set; }
    public DateOnly? AllocationEffectiveFrom { get; set; }
    public decimal? CeilingDays { get; set; }
    public int? FirstYearMonthsPresent { get; set; }

    /// <summary>
    /// The entitlement stored on the balance row. Accrual follows the rulebook, so when the two differ
    /// the statement says so (Repair entitlements brings the row into line).
    /// </summary>
    public decimal StoredEntitledDays { get; set; }

    // The policy.
    public bool HasPolicy { get; set; }
    public AccrualFrequency? Frequency { get; set; }
    public AccrualMode? Mode { get; set; }
    public int? MinServiceMonths { get; set; }
    public bool ProRateOnJoin { get; set; }
    public bool ProRateOnExit { get; set; }

    // The working.
    public DateOnly? HiredOn { get; set; }
    public DateOnly? LeftOn { get; set; }
    public DateOnly? EligibleFrom { get; set; }
    public DateOnly? WindowStart { get; set; }
    public int PeriodsPerYear { get; set; }
    public decimal RatePerPeriod { get; set; }
    public bool RateIsDerived { get; set; }
    public List<LeaveAccrualStatementLineDto> Periods { get; set; } = new();
    public DateOnly? NextPeriodStart { get; set; }
    public DateOnly? NextPeriodEnd { get; set; }
    public bool TailNotCredited { get; set; }

    /// <summary>The days accrued as at <see cref="AsOf"/>: the last line's running total.</summary>
    public decimal AccruedDays { get; set; }
    public bool CapReached { get; set; }
}

/// <summary>One completed accrual period.</summary>
public class LeaveAccrualStatementLineDto
{
    public DateOnly Start { get; set; }
    public DateOnly End { get; set; }
    public decimal Days { get; set; }
    public decimal RunningTotal { get; set; }
    public bool Capped { get; set; }
}

// ─── Leave owed as at a date (round 5, lane C6 — decision A7) ────────────────

/// <summary>
/// Annual leave built up and not yet taken, per employee on the books, as at a chosen date. Days
/// only: Finance puts the money on them.
/// </summary>
public class LeaveOwedReportDto
{
    /// <summary>The date the report is worked out to.</summary>
    public DateOnly AsOf { get; set; }

    /// <summary>The leave year the date falls in, and its bounds.</summary>
    public int Year { get; set; }
    public DateOnly YearStart { get; set; }
    public DateOnly YearEnd { get; set; }

    /// <summary>The tenant's annual leave type — the one leave the report is about.</summary>
    public Guid LeaveTypeId { get; set; }
    public string LeaveTypeName { get; set; } = string.Empty;

    /// <summary>
    /// When carried-in days stop being usable this year (the leave type's carry-over expiry), if they
    /// ever do. On and after it, only carried days taken before it count.
    /// </summary>
    public DateOnly? CarryOverExpiresOn { get; set; }

    public List<LeaveOwedRowDto> Rows { get; set; } = new();
    public LeaveOwedTotalsDto Totals { get; set; } = new();
}

/// <summary>One employee's annual leave as at the report date.</summary>
/// <remarks>
/// <c>Owed = BuiltUp + CarriedIn + Adjustments − Taken − CashedIn</c>. Leave that is approved but not
/// yet taken, and leave awaiting approval, is still owed: the person has not had it. The two are
/// shown beside it so HR can see what is already spoken for.
/// </remarks>
public class LeaveOwedRowDto
{
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public string? StaffNumber { get; set; }
    public string? OrganizationUnitName { get; set; }
    public DateOnly? HiredOn { get; set; }

    /// <summary>The last day of service of somebody who has left since the report date.</summary>
    public DateOnly? LeftOn { get; set; }

    /// <summary>The whole year's entitlement.</summary>
    public decimal EntitledDays { get; set; }

    /// <summary>Built up (accrued) to the report date — or the whole entitlement if the type does not accrue.</summary>
    public decimal BuiltUpDays { get; set; }

    /// <summary>Carried in from last year and still usable at the report date.</summary>
    public decimal CarriedInDays { get; set; }

    /// <summary>HR's adjustments to the year (opening balances, approved deferrals, forfeiture).</summary>
    public decimal AdjustmentDays { get; set; }

    /// <summary>Days of approved leave on or before the report date.</summary>
    public decimal TakenDays { get; set; }

    /// <summary>Days paid out instead of taken.</summary>
    public decimal CashedInDays { get; set; }

    public decimal OwedDays { get; set; }

    /// <summary>Approved leave after the report date — owed, and already spoken for.</summary>
    public decimal BookedDays { get; set; }

    /// <summary>Leave awaiting approval — owed, and asked for.</summary>
    public decimal AwaitingApprovalDays { get; set; }
}

/// <summary>The report's column totals.</summary>
public class LeaveOwedTotalsDto
{
    public int Employees { get; set; }
    public decimal EntitledDays { get; set; }
    public decimal BuiltUpDays { get; set; }
    public decimal CarriedInDays { get; set; }
    public decimal AdjustmentDays { get; set; }
    public decimal TakenDays { get; set; }
    public decimal CashedInDays { get; set; }
    public decimal OwedDays { get; set; }
    public decimal BookedDays { get; set; }
    public decimal AwaitingApprovalDays { get; set; }
}

/// <summary>
/// The tenant's current leave year (round 5, lane C4), so screens can default to it rather than to
/// the calendar year.
/// </summary>
public class LeaveYearInfoDto
{
    public int StartMonth { get; set; }
    public int CurrentYear { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
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

    public DateTime? CancellationDate { get; set; }
    public string? CancellationReason { get; set; }

    /// <summary>
    /// The employee's own reliever roster (active entries, by priority), so whoever opens the plan
    /// can pick relievers from it. Filled on the single-plan read only.
    /// </summary>
    /// <remarks>
    /// Round 5 lane E2/E3. The roster's own endpoint is self-or-HR, so the line manager deciding the
    /// plan could not read it. Carrying it here opens exactly one employee's roster, to whoever may
    /// open that plan — the same reach as the plan itself.
    /// </remarks>
    public List<LeavePlanRosterRelieverDto> RelieverRoster { get; set; } = new();

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

/// <summary>One entry of an employee's reliever roster, as a plan offers it.</summary>
public class LeavePlanRosterRelieverDto
{
    public Guid EmployeeId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? PositionName { get; set; }
    /// <summary>1 = primary, 2 = backup, and so on.</summary>
    public int Priority { get; set; }
}

/// <summary>
/// The approver's one edit to a submitted plan: who covers (round 5 lane E3). Both slots are
/// replaced — send the one to keep as well as the one to change; null empties a slot.
/// </summary>
public class UpdateLeavePlanRelieversDto
{
    public Guid? RelieverId { get; set; }
    public Guid? SecondRelieverId { get; set; }
}

/// <summary>Cancelling a plan. The reason is required when HR cancels an approved one.</summary>
public class CancelLeavePlanDto
{
    public string? Reason { get; set; }
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

    /// <summary>
    /// Charge the days beyond this leave's limit to annual leave, HR deciding (round 5, decision
    /// A5). Allowed only where the leave type says so; see <see cref="LeaveExcessPreviewDto"/>.
    /// </summary>
    public bool ChargeExcessToAnnual { get; set; }
}

/// <summary>
/// What a request for these dates would ask of its leave type, and of annual leave beyond the type's
/// limit (round 5, lane H). Read by the request forms before anything is saved.
/// </summary>
public class LeaveExcessPreviewDto
{
    /// <summary>The days the dates cost on this leave type, counted by its own rules.</summary>
    public decimal RequestedDays { get; set; }

    /// <summary>What the leave type has left that can be taken now.</summary>
    public decimal AvailableDays { get; set; }

    /// <summary>The days beyond what the type can take: 0 when the request fits.</summary>
    public decimal ExcessDays { get; set; }

    /// <summary>Whether the leave type lets those days be charged to annual leave.</summary>
    public bool AllowsOffsetAgainstAnnual { get; set; }

    public string? AnnualLeaveTypeName { get; set; }

    /// <summary>The days annual leave would be charged, counted by annual leave's rules.</summary>
    public decimal? AnnualDays { get; set; }

    /// <summary>What annual leave has left that can be taken now.</summary>
    public decimal? AnnualAvailableDays { get; set; }

    /// <summary>Why the extra days cannot be charged to annual leave, when they cannot.</summary>
    public string? Refusal { get; set; }
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

    /// <summary>The leave type's kind (round 5, A4). Maternity is confirmed, never moved.</summary>
    public LeaveTypeCategory? LeaveTypeCategory { get; set; }
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
    /// True when the request was raised from an APPROVED plan and asks for exactly the plan's dates
    /// (round 5, decision B4). Annual leave that was scheduled is applied for "as of right": it still
    /// goes through both approvals, but the approver can see at a glance that the dates were agreed.
    /// </summary>
    public bool MatchesApprovedPlan { get; set; }

    // ── Beyond the limit, charged to annual leave (round 5, lane H, decision A5) ───────────────
    /// <summary>The employee asked for the days beyond this leave's limit to go to annual leave.</summary>
    public bool ChargeExcessToAnnual { get; set; }

    /// <summary>
    /// While the request is undecided: the days that would be charged to annual leave if it were
    /// approved now, and why they could not be, when they could not. Single read only.
    /// </summary>
    public decimal? ExcessToAnnualDays { get; set; }
    public string? ExcessToAnnualRefusal { get; set; }

    /// <summary>
    /// On a request split at approval: the annual part that took the days beyond the limit. The
    /// absence runs on to its end date. Single read only.
    /// </summary>
    public Guid? ChargedToAnnualRequestId { get; set; }
    public string? ChargedToAnnualRequestNumber { get; set; }
    public string? ChargedToAnnualLeaveTypeName { get; set; }
    public decimal? ChargedToAnnualDays { get; set; }
    public DateOnly? ChargedToAnnualEndDate { get; set; }
    public LeaveStatus? ChargedToAnnualStatus { get; set; }

    /// <summary>
    /// On the annual part of a split: the request it was split from, where the absence began.
    /// Number, type, start and status are filled on the single read only.
    /// </summary>
    public Guid? SplitFromRequestId { get; set; }
    public string? SplitFromRequestNumber { get; set; }
    public string? SplitFromLeaveTypeName { get; set; }
    public DateOnly? SplitFromStartDate { get; set; }
    public LeaveStatus? SplitFromStatus { get; set; }

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
    /// <summary>
    /// The medical board that ruled on this absence, when one sat. ⚠ A bare id across a module
    /// boundary — leave reads the board and never writes it.
    /// </summary>
    public Guid? MedicalBoardId { get; set; }

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

    // Coming back (round 5, B3): what the employee reported, who confirmed it, and how it stood.
    public DateOnly? ResumptionDate { get; set; }
    public DateTime? ResumptionReportedDate { get; set; }
    public Guid? ResumptionReportedById { get; set; }
    public string? ResumptionReportedByName { get; set; }
    public Guid? ClosureConfirmedById { get; set; }
    public string? ClosureConfirmedByName { get; set; }
    public int? OverstayDays { get; set; }

    /// <summary>The first working day after the leave, when the employee is due back. Single read only.</summary>
    public DateOnly? ExpectedReturnDate { get; set; }

    /// <summary><c>Early</c>, <c>OnTime</c> or <c>Late</c> against <see cref="ExpectedReturnDate"/>; null with no resumption date.</summary>
    public string? ResumptionTiming { get; set; }

    /// <summary>What the caller may do with this request. Single read only, decided by the server.</summary>
    public LeaveRequestViewerActionsDto? ViewerActions { get; set; }

    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// The actions a request's screen may offer the person looking at it (round 5, lane D).
/// </summary>
/// <remarks>
/// Decided on the server because two of the rules turn on facts the screen cannot know: whether
/// the viewer is the employee's supervisor or head of department, and today's date against the
/// leave's first day. The endpoints enforce the same rules; this only stops a button being offered
/// that would be refused.
/// </remarks>
public class LeaveRequestViewerActionsDto
{
    public bool CanCancel { get; set; }

    /// <summary>Cancelling approved leave takes back something granted, so it must say why.</summary>
    public bool CancelNeedsReason { get; set; }

    public bool CanRecall { get; set; }
    public bool CanReportResumption { get; set; }
    public bool CanConfirmResumption { get; set; }
}

/// <summary>"I'm back at work" (round 5, B3). The day defaults to today.</summary>
public class ReportResumptionDto
{
    public DateOnly? ResumedOn { get; set; }
}

public class ApproveLeaveDto
{
    public Guid ApprovedBy { get; set; }
    public string? ApprovalNotes { get; set; }
    public string? Comments { get; set; }
}

/// <summary>
/// One row of the annual-leave compliance report: for the tenant's Annual leave type (round 5,
/// A4; it was a <c>MandatoryAnnualLeave</c> flag), how much of an employee's entitlement they have
/// actually taken (used), have scheduled (pending), and still owe within the year.
/// </summary>
public class MandatoryLeaveComplianceDto
{
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;

    /// <summary>So a row can be found, and taken to the person it is about.</summary>
    public string EmployeeNumber { get; set; } = string.Empty;

    /// <summary>
    /// The employee's organisation unit. ⚠ Added for finding L-22: the register listed
    /// everybody with no way to narrow it, and outstanding mandatory leave is something a
    /// DEPARTMENT acts on — it is the head who has to release people, not HR one name at a time.
    /// </summary>
    public Guid? OrganizationUnitId { get; set; }
    public string? OrganizationUnitName { get; set; }
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

/// <summary>
/// Confirming the return, which is what closes the leave (round 5, B3).
/// </summary>
public class CloseLeaveDto
{
    public string? ClosureNotes { get; set; }

    /// <summary>
    /// The first day back, when the confirmer knows better than the report, or there was no report.
    /// Omitted, the employee's reported day stands; with no report either, the return is on time.
    /// </summary>
    public DateOnly? ResumptionDate { get; set; }
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

    /// <summary>
    /// What the document is. ⚠ The evidence gate reads this, not the file name — `scan.pdf` is a
    /// medical certificate or a holiday photograph with equal probability.
    /// </summary>
    public LeaveEvidenceKind EvidenceKind { get; set; }
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

    /// <summary>
    /// How the amount was arrived at, in words. ⚠ Recorded at payout time, not recomputed — the
    /// divisor behind it is a setting, so a figure that cannot name its own basis stops reconciling
    /// the moment somebody edits it.
    /// </summary>
    public string? RateBasis { get; set; }

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

    /// <summary>
    /// Who it goes to (round 5, lane I): <c>Employee</c>, <c>Manager</c>, <c>Confirmer</c>,
    /// <c>Approver</c>, <c>Hr</c> — the audience part of its topic key — and <c>HrDigest</c> when it
    /// is counted into HR's one summary per run. <c>Hr</c> means nobody else could be told.
    /// </summary>
    public List<string> SentTo { get; set; } = new();
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
