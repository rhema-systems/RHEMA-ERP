using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Entities.HR.StaffAttendance;

namespace ErpSystem.Core.Entities.HR.StaffLeave;

/// <summary>
/// Configurable leave types with policies
/// </summary>
public class LeaveType : TenantEntity
{
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(50)]
    public string Code { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    public bool IsPaid { get; set; } = true;

    public int DefaultDaysPerYear { get; set; }

    public int MaxDaysPerYear { get; set; }

    public int? MinDaysNotice { get; set; } // Minimum notice period in days

    public bool RequiresApproval { get; set; } = true;

    [MaxLength(50)]
    public string? CalendarColor { get; set; }

    public bool HasSubTypes { get; set; }

    // Policy constraints
    public bool AllowCarryOver { get; set; }

    public int? MaxCarryOverDays { get; set; }

    public bool CountWeekendsAsLeave { get; set; } = true;

    public bool CountHolidaysAsLeave { get; set; } = false;

    public bool AllowCashConversion { get; set; }

    public bool RequiresReliever { get; set; } = false;

    // ===== ACCESS / ACCRUAL POLICY (Phase 2) =====

    /// <summary>
    /// Minimum months of service from <see cref="Employee.DateEmployed"/> before an employee can
    /// apply for this leave type at all (e.g. annual leave = 12; compassionate/sick = 0/null).
    /// Distinct from the accrual policy's <see cref="LeaveAccrualPolicy.MinServiceMonths"/> which
    /// governs how much has accrued. Null/0 means available from day one.
    /// </summary>
    public int? MinServiceMonthsToAccess { get; set; }

    // ===== CARRY-OVER / FORFEITURE POLICY (Phase 2) =====

    /// <summary>
    /// If carry-over is allowed, how many months into the new year the carried-over balance
    /// remains usable before it expires (e.g. 3 = usable only in Jan–Mar). Null = no expiry.
    /// </summary>
    public int? CarryOverExpiryMonths { get; set; }

    /// <summary>
    /// When set, unused accrued days are forfeited (zeroed) this many months after year start
    /// during the year-end/cut-off processing — the "use it or lose it" / force-leave rule.
    /// </summary>
    public int? ForfeitUnusedAfterMonths { get; set; }

    /// <summary>
    /// ⚠ <b>What the year-end runs COUNT as unused</b> — the days the year granted, or the days the
    /// employee actually earned (entitlement plan B2, decision D-2).
    /// </summary>
    /// <remarks>
    /// <para><b>It governs BOTH year-end acts</b>, carry-over and forfeiture, which is why it is not
    /// called <c>CarryOverBasis</c>: a name that covers half of what a setting does is the kind of
    /// thing this plan exists to stop.</para>
    ///
    /// <para><b>Why it is a choice and not a fix.</b> Both readings are ordinary employer policy.
    /// Under <see cref="LeaveYearEndBasis.Granted"/> — the default, and the behaviour before this
    /// existed — carry-over is computed from <c>LeaveBalance.AvailableDays</c>, which is built on the
    /// whole year's <c>EntitledDays</c>. So somebody who joined in October, accrued 3.5 days and took
    /// none carries the full cap. Under <see cref="LeaveYearEndBasis.Earned"/> they carry 3.5.</para>
    ///
    /// <para>⚠ <b>The default is Granted deliberately.</b> It is today's behaviour, and the year-end
    /// runs have no undo — a default that silently reduced people's carried days on the next run
    /// would be worse than the inconsistency it corrects. The same reasoning gave
    /// <c>AllowInServiceEncashment</c> its conservative default.</para>
    /// </remarks>
    public LeaveYearEndBasis YearEndBasis { get; set; } = LeaveYearEndBasis.Granted;

    /// <summary>
    /// ⚠ <b>Scales the first year's entitlement to the part of the year the employee was here for</b>
    /// (entitlement plan B3, decision D-4). Default <c>false</c> — today's behaviour, where a
    /// December joiner's <c>EntitledDays</c> reads the full annual figure.
    /// </summary>
    /// <remarks>
    /// <para>⚠ <b>It cannot be combined with incremental accrual, and the service refuses the
    /// combination rather than ignoring one of them.</b> Incremental accrual already limits a joiner
    /// to the part of the year they were present for — it opens the accrual window at their hire
    /// date. Scaling the entitlement as well deducts for the same months twice, and because the
    /// derived per-period rate is itself <c>entitlement ÷ periods</c>, it would deduct a third time
    /// through the rate.</para>
    ///
    /// <para>So this is for leave types that <b>grant</b> rather than accrue: no accrual policy, or
    /// one set to full-grant-on-eligibility. That is the mirror of <c>ProRateOnExit</c>, which
    /// applies to incremental accrual <i>only</i>.</para>
    /// </remarks>
    public bool ProRateFirstYearEntitlement { get; set; }

    /// <summary>
    /// Marks the leave type as mandatory-to-take within the year (force leave). Surfaced to HR
    /// and used by the forfeiture routine.
    /// </summary>
    public bool MandatoryAnnualLeave { get; set; }

    // ===== ENCASHMENT RATE POLICY (Phase 4) =====

    /// <summary>
    /// How the per-day encashment rate is derived for this leave type:
    /// <see cref="EncashmentRateBasis.DerivedFromEmoluments"/> = (monthly basic + linked
    /// allowances) / <see cref="EncashmentWorkingDaysPerMonth"/>; or
    /// <see cref="EncashmentRateBasis.Manual"/> = the fixed <see cref="EncashmentRatePerDay"/>.
    /// </summary>
    public EncashmentRateBasis EncashmentRateBasis { get; set; } = EncashmentRateBasis.DerivedFromEmoluments;

    /// <summary>Manual per-day encashment rate, used when <see cref="EncashmentRateBasis"/> is Manual.</summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal? EncashmentRatePerDay { get; set; }

    /// <summary>Working-days-per-month divisor used to turn a monthly emolument into a daily rate.</summary>
    public int EncashmentWorkingDaysPerMonth { get; set; } = 22;

    // ── Medical evidence (residue plan R-15a) ────────────────────────────────────────────────
    //
    // Excuse duty, and the medical board. Raised by stakeholders; nothing existed before this —
    // sick leave was an ordinary leave type with optional untyped attachments, so no rule could be
    // written about what had to be produced or when.
    //
    // ⚠ All three live on the LEAVE TYPE, not on the tenant, because they are rules about A KIND OF
    // LEAVE. Sick leave needs a certificate; annual leave does not. A tenant-wide setting could not
    // express that, and every client has both kinds.
    //
    // ⚠ The numbers below are DEFAULTS, not rules from any authority. They are inert until
    // RequiresMedicalCertificate is switched on, and each client sets what its own policy says. The
    // same reasoning as CompanyHrPolicySettings.GrievanceRungChaseDays: a working assumption that
    // costs a settings edit to change rather than a release.

    /// <summary>
    /// Whether this leave type requires a medical certificate — <b>excuse duty</b> — once the
    /// self-certification period is passed.
    /// </summary>
    /// <remarks>
    /// Defaults <b>false</b>, so no existing leave type starts refusing requests that were fine
    /// yesterday. Turn it on for sick leave and its relatives; leave it off for everything else.
    /// </remarks>
    public bool RequiresMedicalCertificate { get; set; } = false;

    /// <summary>
    /// Days an employee may take on their own word before <see cref="RequiresMedicalCertificate"/>
    /// bites. A request of this length or shorter needs no certificate.
    /// </summary>
    /// <remarks>
    /// ⚠ <b>3 is a starting value, not a rule.</b> Two to three days is common Ghanaian practice and
    /// it is what the residue plan inferred; TDC has not confirmed it and neither has anyone else.
    /// Set 0 to require a certificate for even a single day.
    /// </remarks>
    [Range(0, 365)]
    public int SelfCertificationDays { get; set; } = 3;

    /// <summary>
    /// Cumulative days of this leave type, in one leave year, beyond which a <b>medical board</b>
    /// recommendation must be attached. Null means no board is ever required.
    /// </summary>
    /// <remarks>
    /// <para>⚠ <b>Cumulative across the year, not per request</b>, and that is the whole point — a
    /// per-request threshold is defeated by splitting one long absence into several short ones. The
    /// same reasoning that made the sub-type cap annual in the closure build (decision D-2).</para>
    ///
    /// <para>⚠ 90 days is a starting value inferred from general practice, not a figure any
    /// authority has given us. It is logged for TDC as <b>L-D10</b>. Null it out if a client has no
    /// board at all.</para>
    /// </remarks>
    [Range(1, 365)]
    public int? MedicalBoardThresholdDays { get; set; } = 90;

    public bool IsActive { get; set; } = true;

    // Navigation properties
    public virtual List<LeaveCategoryAllocation> LeaveCategoryAllocations { get; set; } = new List<LeaveCategoryAllocation>();
    public virtual List<LeaveSubType> LeaveSubTypes { get; set; } = new List<LeaveSubType>();
    public virtual ICollection<LeaveTypeEligibility> EligibilityRules { get; set; } = new List<LeaveTypeEligibility>();
    public virtual ICollection<LeaveAccrualPolicy> AccrualPolicies { get; set; } = new List<LeaveAccrualPolicy>();
    public virtual ICollection<LeaveBalance> LeaveBalances { get; set; } = new List<LeaveBalance>();
    public virtual ICollection<LeaveRequest> LeaveRequests { get; set; } = new List<LeaveRequest>();

    /// <summary>Allowance pay components that feed this leave type's derived encashment rate (Phase 4).</summary>
    public virtual ICollection<LeaveTypeAllowance> LeaveTypeAllowances { get; set; } = new List<LeaveTypeAllowance>();
}

/// <summary>
/// Join row linking a leave type to an allowance <see cref="PayComponent"/> whose value is added
/// to basic pay when deriving the leave type's per-day encashment rate (Phase 4, comment 13).
/// </summary>
public class LeaveTypeAllowance : TenantEntity
{
    public Guid LeaveTypeId { get; set; }

    public Guid PayComponentId { get; set; }

    [ForeignKey(nameof(LeaveTypeId))]
    public virtual LeaveType LeaveType { get; set; } = null!;

    [ForeignKey(nameof(PayComponentId))]
    public virtual PayComponent PayComponent { get; set; } = null!;
}

/// <summary>
/// Represents specific subtypes of leave under a general leave type.
/// </summary>
public class LeaveSubType : TenantEntity
{
    public Guid LeaveTypeId { get; set; }

    [Required]
    [MaxLength(100)]
    public string SubTypeName { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    /// <summary>
    /// Caps days for this subtype. Takes precedence over LeaveCategoryAllocation
    /// when both exist — enforce this rule in the domain/service layer.
    /// </summary>
    public int? MaxDaysAllowed { get; set; }

    public bool IsActive { get; set; } = true;

    [ForeignKey(nameof(LeaveTypeId))]
    public virtual LeaveType LeaveType { get; set; } = null!;

    public virtual List<LeaveCategoryAllocation> LeaveCategoryAllocations { get; set; } = new List<LeaveCategoryAllocation>();
    public virtual ICollection<LeaveBalance> LeaveBalances { get; set; } = new List<LeaveBalance>();
    public virtual ICollection<LeaveRequest> LeaveRequests { get; set; } = new List<LeaveRequest>();
    public virtual ICollection<LeavePlan> LeavePlans { get; set; } = new List<LeavePlan>();
}

/// <summary>
/// Leave day allocations by staff level, optionally scoped to a subtype.
/// When LeaveSubType.MaxDaysAllowed is also set, the subtype cap takes precedence.
/// </summary>
public class LeaveCategoryAllocation : TenantEntity
{
    public Guid LeaveTypeId { get; set; }

    public Guid? LeaveSubTypeId { get; set; }

    public Guid StaffLevelId { get; set; }

    public int AllocationDays { get; set; }

    public DateOnly EffectiveFrom { get; set; }
    
    public DateOnly? EffectiveTo { get; set; }

    [ForeignKey(nameof(LeaveTypeId))]
    public virtual LeaveType LeaveType { get; set; } = null!;

    [ForeignKey(nameof(LeaveSubTypeId))]
    public virtual LeaveSubType? LeaveSubType { get; set; }

    [ForeignKey(nameof(StaffLevelId))]
    public virtual StaffLevel StaffLevel { get; set; } = null!;
}

public class LeaveTypeEligibility : TenantEntity
{
    public Guid LeaveTypeId { get; set; }

    public LeaveEligibilityType EligibilityType { get; set; }

    public Guid? OrganizationLevelId { get; set; }

    public Guid? OrganizationUnitId { get; set; }

    public Guid? PositionId { get; set; }

    public Gender? Gender { get; set; }

    [ForeignKey(nameof(LeaveTypeId))]
    public virtual LeaveType LeaveType { get; set; } = null!;

    [ForeignKey(nameof(OrganizationLevelId))]
    public virtual OrganizationLevel? OrganizationLevel { get; set; }

    [ForeignKey(nameof(OrganizationUnitId))]
    public virtual OrganizationUnit? OrganizationUnit { get; set; }

    [ForeignKey(nameof(PositionId))]
    public virtual EmployeePosition? Position { get; set; }
}

public class LeaveAccrualPolicy : TenantEntity
{
    public Guid LeaveTypeId { get; set; }

    public AccrualFrequency Frequency { get; set; } = AccrualFrequency.Monthly;

    /// <summary>
    /// How the annual entitlement is released over the year (incrementally vs full-on-eligibility).
    /// </summary>
    public AccrualMode Mode { get; set; } = AccrualMode.AccrueIncrementally;

    [Column(TypeName = "decimal(5,2)")]
    public decimal AccrualRate { get; set; }

    public int? MinServiceMonths { get; set; } // Minimum months of service before accrual starts

    public bool ProRateOnJoin { get; set; }

    public bool ProRateOnExit { get; set; }

    public bool IsActive { get; set; } = true;

    [ForeignKey(nameof(LeaveTypeId))]
    public virtual LeaveType LeaveType { get; set; } = null!;
}

/// <summary>
/// Employee leave balances per leave type per year
/// </summary>
public class LeaveBalance : TenantEntity
{
    public Guid EmployeeId { get; set; }

    public Guid LeaveTypeId { get; set; }

    public Guid? LeaveSubTypeId { get; set; }

    public int Year { get; set; }

    // ===== CORE VALUES (FROM POLICY / SYSTEM) =====
    
    [Column(TypeName = "decimal(5,2)")]
    public decimal EntitledDays { get; set; }

    [Column(TypeName = "decimal(5,2)")]
    public decimal CarriedOverDays { get; set; }

    // ===== COMPUTED / AGGREGATED VALUES (CACHED) =====
    
    [Column(TypeName = "decimal(5,2)")]
    public decimal UsedDays { get; set; } // From approved LeaveRequests

    [Column(TypeName = "decimal(5,2)")]
    public decimal PendingDays { get; set; } // From pending LeaveRequests

    [Column(TypeName = "decimal(5,2)")]
    public decimal AdjustmentDays { get; set; } // SUM of LeaveAdjustment.Days

    [Column(TypeName = "decimal(5,2)")]
    public decimal EncashedDays { get; set; } // SUM of processed LeaveEncashment.DaysEncashed

    // ===== COMPUTED PROPERTY (DO NOT STORE IN DB) =====

    [NotMapped]
    public decimal AvailableDays => EntitledDays + CarriedOverDays + AdjustmentDays - UsedDays - PendingDays - EncashedDays;

    [ForeignKey(nameof(EmployeeId))]
    public virtual Employee Employee { get; set; } = null!;

    [ForeignKey(nameof(LeaveTypeId))]
    public virtual LeaveType LeaveType { get; set; } = null!;

    [ForeignKey(nameof(LeaveSubTypeId))]
    public virtual LeaveSubType? LeaveSubType { get; set; }

    public virtual ICollection<LeaveAdjustment> Adjustments { get; set; } = new List<LeaveAdjustment>();
}

/// <summary>
/// Represents an adjustment to an employee's leave balance
/// </summary>
public class LeaveAdjustment : TenantEntity
{
    public Guid EmployeeId { get; set; }

    public Guid LeaveTypeId { get; set; }

    public Guid? LeaveSubTypeId { get; set; }

    public int Year { get; set; }

    [Column(TypeName = "decimal(5,2)")]
    public decimal Days { get; set; } // + or -

    /// <summary>Optional structured reason from the system-wide <see cref="ReasonCode"/> lookup.</summary>
    public Guid? ReasonCodeId { get; set; }

    /// <summary>Free-text remarks (surfaced in the UI as "Remarks" alongside the reason code).</summary>
    [MaxLength(500)]
    public string Reason { get; set; } = string.Empty;

    public DateTime AdjustmentDate { get; set; }

    public Guid PerformedBy { get; set; }

    public Guid LeaveBalanceId { get; set; }

    [ForeignKey(nameof(LeaveBalanceId))]
    public virtual LeaveBalance LeaveBalance { get; set; } = null!;

    [ForeignKey(nameof(PerformedBy))]
    public virtual Employee PerformedByEmployee { get; set; } = null!;

    [ForeignKey(nameof(ReasonCodeId))]
    public virtual ReasonCode? ReasonCode { get; set; }
}

/// <summary>
/// Represents an employee's planned leave schedule within a specific year
/// </summary>
public class LeavePlan : TenantEntity
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

    /// <summary>Second reliever/backup; pre-defined relievers populate both slots by priority.</summary>
    public Guid? SecondRelieverId { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }

    public Guid PlannedBy { get; set; }

    public int Year { get; set; }

    public LeavePlanStatus Status { get; set; } = LeavePlanStatus.Draft;

    // Self-service proposal flow — manager-suggested alternative dates (status ChangesSuggested)
    public DateOnly? SuggestedStartDate { get; set; }
    public DateOnly? SuggestedEndDate { get; set; }

    [MaxLength(1000)]
    public string? ManagerSuggestionNotes { get; set; }

    // Workflow integration
    public Guid? WorkflowInstanceId { get; set; }
    public Guid? ApprovedById { get; set; }
    public DateTime? ApprovedDate { get; set; }
    public string? RejectionReason { get; set; }

    [ForeignKey(nameof(EmployeeId))]
    public virtual Employee Employee { get; set; } = null!;

    [ForeignKey(nameof(OrganizationLevelId))]
    public virtual OrganizationLevel? OrganizationLevel { get; set; }

    [ForeignKey(nameof(OrganizationUnitId))]
    public virtual OrganizationUnit? OrganizationUnit { get; set; }

    [ForeignKey(nameof(PositionId))]
    public virtual EmployeePosition? Position { get; set; }

    [ForeignKey(nameof(LeaveTypeId))]
    public virtual LeaveType LeaveType { get; set; } = null!;

    [ForeignKey(nameof(LeaveSubTypeId))]
    public virtual LeaveSubType? LeaveSubType { get; set; }

    [ForeignKey(nameof(RelieverId))]
    public virtual Employee? RelieverEmployee { get; set; }

    [ForeignKey(nameof(SecondRelieverId))]
    public virtual Employee? SecondRelieverEmployee { get; set; }

    [ForeignKey(nameof(PlannedBy))]
    public virtual Employee? PlannedByEmployee { get; set; }
}

/// <summary>
/// Employee leave application
/// </summary>
public class LeaveRequest : TenantEntity
{
    [MaxLength(50)]
    public string RequestNumber { get; set; } = string.Empty;

    public Guid EmployeeId { get; set; }

    public Guid LeaveTypeId { get; set; }

    public Guid? LeaveSubTypeId { get; set; }

    public DateOnly StartDate { get; set; }

    public DateOnly EndDate { get; set; }

    [Column(TypeName = "decimal(5,2)")]
    public decimal TotalDays { get; set; } // Calculated based on policy

    public DateTime RequestDate { get; set; }

    public Guid? LeavePlanId { get; set; }

    [Required]
    [MaxLength(1000)]
    public string Reason { get; set; } = string.Empty;

    public LeaveStatus Status { get; set; } = LeaveStatus.Pending;

    // ── Send back with different dates (status ChangesSuggested) ─────────────────────────────
    // The mirror of LeavePlan's three fields. A leave PLAN could always be sent back with the
    // manager's own dates; a leave REQUEST — the record that actually books the days — could only
    // be approved or rejected outright, so TDC's "sending back for correction with suggested dates"
    // had nowhere to happen (closure plan R-3 / decision D-1).
    public DateOnly? SuggestedStartDate { get; set; }
    public DateOnly? SuggestedEndDate { get; set; }

    [MaxLength(1000)]
    public string? ManagerSuggestionNotes { get; set; }

    // ── Rescheduling an already-approved request ─────────────────────────────────────────────
    // TDC asked for "possible shifting of the leave to a different day even after the planning is
    // done". The only route used to be cancel-and-re-key, which loses the number, the approval and
    // the history (closure plan R-8 / decision D-5). These record the move rather than hide it;
    // moving the dates re-opens the approval, so the record never claims an approval of dates
    // nobody approved.
    //
    // ⚠ No navigation properties on the two actor columns, deliberately. They match ApprovedById
    // above, which is also a bare Guid: an unpaired navigation to Employee mints a shadow
    // `EmployeeId1` column on the other side. Names are resolved in the read that needs them.
    public DateOnly? OriginalStartDate { get; set; }
    public DateOnly? OriginalEndDate { get; set; }
    public DateTime? RescheduledDate { get; set; }
    public Guid? RescheduledById { get; set; }

    [MaxLength(500)]
    public string? RescheduleReason { get; set; }

    /// <summary>How many times the approved dates have been moved. Zero for a request never moved.</summary>
    public int RescheduleCount { get; set; }

    // ── "Is this still going ahead?" ─────────────────────────────────────────────────────────
    // An approved request used to go quiet between approval and its start date: nobody was asked
    // whether the person was still going, and nothing recorded the answer (closure plan R-7 /
    // decision D-4). The reminder that asks is the leave reminder service; this is where the answer
    // lands. Deferring is not a separate state — it is a reschedule, above.
    public DateTime? ObservanceConfirmedDate { get; set; }
    public Guid? ObservanceConfirmedById { get; set; }

    // ── Recall from leave (curtailment) ──────────────────────────────────────────────────────
    // The employer calls somebody back before their end date (residue plan R-14). Deliberately NOT
    // a cancellation, NOT an amendment and NOT a reschedule, because none of those record the fact:
    //
    //   Cancel     releases every day, including the ones already taken.
    //   Close      refuses before the end date.
    //   Reschedule says the leave MOVED. Recalled leave did not move — it was interrupted.
    //
    // So curtailment TRUNCATES: days up to the recall stand as taken, days after are restored to
    // the balance, and the request keeps its number and its approval, because the leave was validly
    // granted and then cut short. That distinction is not pedantry — restored days are often
    // protected from the normal carry-over expiry, and recall costs can be reimbursable, and
    // neither rule can be written against a record that says "cancelled".
    //
    // ⚠ The pre-recall end date is its own column rather than reusing OriginalEndDate above.
    // OriginalEndDate means "the dates before the first reschedule"; conflating a move with an
    // interruption would make both unreadable, and a request can be rescheduled AND later recalled.
    //
    // ⚠ No navigation on RecalledById — same reason as RescheduledById and ApprovedById above.
    public DateOnly? RecallEffectiveDate { get; set; }

    /// <summary>The end date the request carried before it was recalled. Null if never recalled.</summary>
    public DateOnly? PreRecallEndDate { get; set; }

    public DateTime? RecalledDate { get; set; }
    public Guid? RecalledById { get; set; }

    [MaxLength(500)]
    public string? RecallReason { get; set; }

    /// <summary>
    /// The medical board that ruled on this absence, when one sat (residue plan G4).
    /// </summary>
    /// <remarks>
    /// <para>⚠ <b>A bare Guid across a module boundary, with no navigation and no foreign key.</b>
    /// The board is a Medical-module record and the bridge is <b>by reference, one way</b>: leave
    /// READS it to satisfy the board rule, and never writes to it. A navigation would make the board
    /// part of leave's object graph, and an EF fixup would then be able to modify a clinical record
    /// through a leave save — which is exactly the coupling the SHE↔Medical boundary exists to
    /// prevent.</para>
    ///
    /// <para>The typed <c>MedicalBoardRecommendation</c> attachment remains the other way to satisfy
    /// the rule, for clients who hold their boards on paper.</para>
    /// </remarks>
    public Guid? MedicalBoardId { get; set; }

    /// <summary>
    /// Days handed back to the balance by the recall. Recorded rather than derived: the balance
    /// re-derives itself from <see cref="TotalDays"/>, but "how many days did this recall return"
    /// is a fact about the event that nothing else preserves once the dates are truncated.
    /// </summary>
    public decimal? DaysRestored { get; set; }

    // Workflow integration
    public Guid? WorkflowInstanceId { get; set; }
    public Guid? ApprovedById { get; set; }
    public DateTime? ApprovedDate { get; set; }
    public string? RejectionReason { get; set; }

    // Reliever/covering staff
    public Guid? RelieverEmployeeId { get; set; }

    /// <summary>Second reliever/backup; pre-defined relievers populate both slots by priority.</summary>
    public Guid? SecondRelieverEmployeeId { get; set; }

    [MaxLength(500)]
    public string? RelieverNotes { get; set; }

    [MaxLength(2000)]
    public string? HandoverNotes { get; set; } // Work handover details

    // Closure
    public DateTime? ClosureDate { get; set; }

    [MaxLength(500)]
    public string? ClosureNotes { get; set; }

    public DateTime? CancellationDate { get; set; }

    [MaxLength(500)]
    public string? CancellationReason { get; set; }

    [ForeignKey(nameof(EmployeeId))]
    public virtual Employee Employee { get; set; } = null!;

    [ForeignKey(nameof(LeaveTypeId))]
    public virtual LeaveType LeaveType { get; set; } = null!;

    [ForeignKey(nameof(LeaveSubTypeId))]
    public virtual LeaveSubType? LeaveSubType { get; set; }

    [ForeignKey(nameof(RelieverEmployeeId))]
    public virtual Employee? RelieverEmployee { get; set; }

    [ForeignKey(nameof(SecondRelieverEmployeeId))]
    public virtual Employee? SecondRelieverEmployee { get; set; }

    [ForeignKey(nameof(LeavePlanId))]
    public virtual LeavePlan? LeavePlan { get; set; }

    /// <summary>Cash conversion record if leave was encashed</summary>
    public virtual LeaveEncashment? Encashment { get; set; }

    public virtual ICollection<LeaveRequestAttachment> Attachments { get; set; } = new List<LeaveRequestAttachment>();

    /// <summary>Attendance days that were recorded as leave against this request.</summary>
    public virtual ICollection<StaffDailyAttendance> AttendanceDays { get; set; } = new List<StaffDailyAttendance>();
}

public class LeaveRequestAttachment : TenantEntity
{
    public Guid LeaveRequestId { get; set; }

    [MaxLength(500)]
    public string FileName { get; set; } = string.Empty;

    [MaxLength(500)]
    public string FilePath { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? ContentType { get; set; }

    public long? FileSizeBytes { get; set; }

    /// <summary>
    /// What this document is — a supporting file, excuse duty, or a medical board recommendation
    /// (residue plan R-15a).
    /// </summary>
    /// <remarks>
    /// ⚠ Without this, the evidence gate could not exist. A rule saying "a certificate must be
    /// attached" needs to distinguish a certificate from any other file, and a filename cannot do
    /// it: <c>scan.pdf</c> is a medical certificate or a holiday photograph with equal probability.
    /// <para>Defaults to <see cref="LeaveEvidenceKind.Other"/>, which is what every attachment
    /// uploaded before this column existed genuinely was.</para>
    /// </remarks>
    public LeaveEvidenceKind EvidenceKind { get; set; } = LeaveEvidenceKind.Other;

    public DateTime UploadedDate { get; set; }

    public Guid UploadedBy { get; set; }

    /// <summary>Scanned controlled upload backing this attachment.</summary>
    public Guid? FileUploadRecordId { get; set; }

    /// <summary>Central-DMS record, once registered.</summary>
    public Guid? DocumentRecordId { get; set; }

    /// <summary>Central-DMS version, once registered.</summary>
    public Guid? DocumentVersionId { get; set; }

    [ForeignKey(nameof(LeaveRequestId))]
    public virtual LeaveRequest LeaveRequest { get; set; } = null!;

    [ForeignKey(nameof(UploadedBy))]
    public virtual Employee UploadedByEmployee { get; set; } = null!;
}

/// <summary>
/// Records a cash conversion transaction when AllowCashConversion is true on the LeaveType.
/// </summary>
public class LeaveEncashment : TenantEntity
{
    public Guid LeaveRequestId { get; set; }

    public Guid EmployeeId { get; set; }

    public Guid LeaveTypeId { get; set; }

    public int Year { get; set; }

    [Column(TypeName = "decimal(5,2)")]
    public decimal DaysEncashed { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal AmountPaid { get; set; }

    /// <summary>
    /// How <see cref="AmountPaid"/> was arrived at, in words — the monthly figure, the divisor, the
    /// resulting daily rate, and where that divisor came from.
    /// </summary>
    /// <remarks>
    /// ⚠ Stored rather than derived on read, and that is the point. Leave encashment and a final
    /// settlement use deliberately different bases — working days per month here, calendar days per
    /// year there, roughly 38% apart on the same salary — and either can be re-configured at any
    /// time. A payout that cannot say which basis produced it becomes unauditable the moment
    /// somebody edits a setting, because the figure no longer reconciles with the live rule and
    /// nothing records the rule that was live when it was paid. The final settlement has recorded
    /// its basis this way since FR-HR-184; leave does now too (residue plan G2).
    /// </remarks>
    [MaxLength(500)]
    public string? RateBasis { get; set; }

    public LeaveEncashmentStatus Status { get; set; } = LeaveEncashmentStatus.Draft;

    // Set after payment is made (null until then)
    public DateTime? ProcessedDate { get; set; }

    public Guid? ProcessedByEmployeeId { get; set; }

    [MaxLength(500)]
    public string? PaymentReference { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }

    // Workflow integration
    public Guid? WorkflowInstanceId { get; set; }
    public Guid? ApprovedById { get; set; }
    public DateTime? ApprovedDate { get; set; }
    public string? RejectionReason { get; set; }

    [ForeignKey(nameof(LeaveRequestId))]
    public virtual LeaveRequest LeaveRequest { get; set; } = null!;

    [ForeignKey(nameof(EmployeeId))]
    public virtual Employee Employee { get; set; } = null!;

    [ForeignKey(nameof(ProcessedByEmployeeId))]
    public virtual Employee? ProcessedByEmployee { get; set; }
}

/// <summary>
/// Pre-defined reliever for an employee, configured on the employee profile. Leave plans and
/// requests auto-populate their reliever slots from these (by <see cref="Priority"/>); the user
/// can still override when there is a clash. Tenant-scoped.
/// </summary>
public class EmployeeReliever : TenantEntity
{
    public Guid EmployeeId { get; set; }

    public Guid RelieverEmployeeId { get; set; }

    /// <summary>1 = primary reliever, 2 = backup. Drives which leave slot it fills.</summary>
    public int Priority { get; set; } = 1;

    public bool IsActive { get; set; } = true;

    [ForeignKey(nameof(EmployeeId))]
    public virtual Employee Employee { get; set; } = null!;

    [ForeignKey(nameof(RelieverEmployeeId))]
    public virtual Employee RelieverEmployee { get; set; } = null!;
}

// ─── Leave reminder engine (closure plan wave E, slice E2) ───────────────────

/// <summary>
/// One execution of the leave reminder sweep.
/// </summary>
/// <remarks>
/// HR has eleven of these engines — assets, certifications, discipline, ID expiry, probation,
/// separation, SHE, movements, travel, teams, attendance — and leave, the module with more dates
/// that matter than any of them, had none. Nothing was raised when leave was about to start, when
/// approved leave was never closed, when mandatory leave went untaken, or when carry-over was about
/// to expire (closure plan R-6 / R-10 / L-23).
/// </remarks>
public class LeaveReminderRun : TenantEntity
{
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    /// <summary>"Scheduled" (background service) or "Manual" (run-now endpoint).</summary>
    [MaxLength(20)]
    public string Trigger { get; set; } = "Scheduled";

    public Guid? TriggeredByUserId { get; set; }

    public int RemindersQueued { get; set; }

    public virtual ICollection<LeaveReminderDispatchLog> DispatchLogs { get; set; }
        = new List<LeaveReminderDispatchLog>();
}

/// <summary>
/// One reminder actually dispatched by a leave sweep.
/// </summary>
/// <remarks>
/// <para>The unique <c>(TenantId, DedupeKey)</c> index is the send-once guarantee: a key encodes
/// the record, the reminder kind, the date it is about and the escalation tier reached, so each
/// rung fires exactly once — and moving a date produces fresh keys, which re-arms the ladder. That
/// is deliberate: leave that has been rescheduled genuinely is a new thing to chase.</para>
///
/// <para>⚠ Nothing here carries a reason for leave, a diagnosis, or a balance. A reminder travels
/// further than the record it is about — into notification lists and email — and "Ama's leave
/// starts on Monday" is actionable without saying why she is going. Sick leave makes this a
/// medical-confidentiality matter, not just a style preference: the same reasoning governs the
/// travel engine's note about passport numbers.</para>
/// </remarks>
public class LeaveReminderDispatchLog : TenantEntity
{
    public Guid RunId { get; set; }

    [ForeignKey(nameof(RunId))]
    public virtual LeaveReminderRun Run { get; set; } = null!;

    /// <summary>
    /// Machine kind: "LeaveStartingSoon", "LeaveNotClosed", "MandatoryLeaveOutstanding",
    /// "CarryOverExpiring", "RequestAwaitingDecision".
    /// </summary>
    [MaxLength(60)]
    public string Kind { get; set; } = string.Empty;

    /// <summary>Human label for the swept item, e.g. "Leave request", "Carry-over".</summary>
    [MaxLength(100)]
    public string ItemType { get; set; } = string.Empty;

    /// <summary>Id of the swept record. No FK — the target table varies by kind.</summary>
    public Guid EntityId { get; set; }

    /// <summary>The employee the reminder is about, so a feed can be scoped to a person.</summary>
    public Guid? EmployeeId { get; set; }

    /// <summary>What the notification shows: a request number and a leave type, and nothing more.</summary>
    [MaxLength(250)]
    public string Reference { get; set; } = string.Empty;

    public DateTime? DueDate { get; set; }

    /// <summary>Days remaining at dispatch time; negative when overdue.</summary>
    public int DaysRemaining { get; set; }

    /// <summary>0 for a due-soon rung; 1, 2 or 3 for an overdue escalation tier.</summary>
    public int EscalationTier { get; set; }

    [Required]
    [MaxLength(300)]
    public string DedupeKey { get; set; } = string.Empty;
}
