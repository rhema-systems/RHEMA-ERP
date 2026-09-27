using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Interfaces.HR;

/// <summary>
/// Point-in-time view of an employee's entitlement for one leave type / year.
/// </summary>
public class LeaveEntitlementSnapshot
{
    /// <summary>
    /// Full annual entitlement (the yearly cap): the staff-level allocation, else the type's default,
    /// under the annual ceiling. Never a sub-type's — it said "subtype/allocation/default" until leave
    /// settings audit 2 (L-96); a sub-type draws on its type's days.
    /// </summary>
    public decimal AnnualEntitledDays { get; set; }

    /// <summary>Days accrued by the as-of date, per the accrual policy. Capped at the annual entitlement.</summary>
    public decimal AccruedToDateDays { get; set; }

    /// <summary>
    /// The date <see cref="AccruedToDateDays"/> is worked out to (round 5, lane C2): the date asked
    /// for, or the year end or the employee's last day when accrual stopped there. <c>null</c> when
    /// the leave type does not accrue, because then the figure is the whole entitlement on any date.
    /// </summary>
    /// <remarks>
    /// It comes from the server rather than being guessed on a screen, because two of the three
    /// limits are the employee's own — a leaver's clock stops on their last day — and a screen that
    /// assumed "today" would label a leaver's figure with a date it was never worked out to.
    /// </remarks>
    public DateOnly? AccruedAsOf { get; set; }

    /// <summary>True when an active accrual policy governs this leave type (otherwise the full entitlement is available immediately).</summary>
    public bool HasAccrualPolicy { get; set; }

    /// <summary>True when the employee has met the service gate and may apply for this leave type.</summary>
    public bool IsAccessible { get; set; }

    /// <summary>The date from which the employee may first apply for this leave type (service gate), if known.</summary>
    public DateOnly? AccessibleFrom { get; set; }

    /// <summary>
    /// ⚠ <b>The one definition of "days this person can actually use right now".</b> Accrued-to-date
    /// (or the full entitlement for a leave type that does not accrue) plus carry-over and
    /// adjustments, less what is used, pending or encashed.
    /// </summary>
    /// <remarks>
    /// <para>It lives here, on the snapshot, because <b>three</b> callers need it and a formula
    /// copied three times is a formula that drifts twice: the create check (what it refuses on),
    /// every balance read (the <i>Can take now</i> column), and — since the entitlement plan's W2b —
    /// the year-end runs when a leave type counts <c>Earned</c> rather than <c>Granted</c>.</para>
    ///
    /// <para>The <c>HasAccrualPolicy</c> test is the part worth not re-deriving: a leave type with no
    /// accrual policy hands over its whole entitlement immediately, so for those the accrued figure
    /// is the entitlement and substituting it would read as zero.</para>
    /// </remarks>
    public decimal AvailableFrom(
        decimal entitled, decimal carried, decimal adjustments,
        decimal used, decimal pending, decimal encashed)
    {
        var effectiveAccrued = HasAccrualPolicy ? AccruedToDateDays : entitled;
        return effectiveAccrued + carried + adjustments - used - pending - encashed;
    }
}

/// <summary>
/// The facts about one employee that accrual reads (round 5, lane C): hire date, last day and staff
/// level. Loaded once by whoever asks — one employee, or every employee on a report.
/// </summary>
/// <param name="EmployeeId">The employee.</param>
/// <param name="HiredOn">The hire date, if recorded. None means eligible from the start of the leave year.</param>
/// <param name="LeftOn">The last day of service, if they have left.</param>
/// <param name="StaffLevelId">The staff level of their position, which picks the allocation.</param>
public sealed record LeaveAccrualSubject(Guid EmployeeId, DateOnly? HiredOn, DateOnly? LeftOn, Guid? StaffLevelId);

/// <summary>
/// One accrual period that has been credited: its dates, the days it added and the total so far.
/// </summary>
public sealed class LeaveAccrualPeriod
{
    public DateOnly Start { get; init; }
    public DateOnly End { get; init; }

    /// <summary>The days this period added. Less than the rate when the cap stopped it.</summary>
    public decimal Days { get; init; }

    /// <summary>The total credited once this period completed.</summary>
    public decimal RunningTotal { get; init; }

    /// <summary>The cap (the year's entitlement) limited what this period could add.</summary>
    public bool Capped { get; init; }
}

/// <summary>
/// How an employee's accrual for a leave year is worked out, step by step (round 5, lane C2): the
/// rule, the entitlement and where it came from, the rate, and one line per completed period.
/// </summary>
/// <remarks>
/// ⚠ This IS the arithmetic, not an account written beside it. <c>LeaveEntitlementService</c> computes
/// every accrued figure — the create check's, the balance screens', the year-end's and the "leave
/// owed" report's — through the same method that fills this in, so the lines of a statement always
/// add up to the figure the rest of the module uses.
/// </remarks>
public sealed class LeaveAccrualWorking
{
    public int Year { get; init; }
    public DateOnly YearStart { get; init; }
    public DateOnly YearEnd { get; init; }

    /// <summary>The date asked for (today when none was given).</summary>
    public DateOnly RequestedAsOf { get; init; }

    /// <summary>The date it is actually worked out to.</summary>
    public DateOnly AsOf { get; init; }

    /// <summary>Why <see cref="AsOf"/> is earlier than <see cref="RequestedAsOf"/>, if it is.</summary>
    public LeaveAccrualAsOfLimit AsOfLimit { get; init; }

    public LeaveAccrualState State { get; init; }

    // ── the entitlement: the cap, and for a derived rate its source ──

    /// <summary>The year's entitlement, which accrual never exceeds.</summary>
    public decimal AnnualEntitledDays { get; init; }
    public LeaveEntitlementSource EntitlementSource { get; init; }

    /// <summary>The allocation's or the default's days, before the ceiling and first-year pro-rating.</summary>
    public decimal EntitlementBaseDays { get; init; }
    public Guid? StaffLevelId { get; init; }
    public string? StaffLevelName { get; init; }
    public DateOnly? AllocationEffectiveFrom { get; init; }

    /// <summary>The annual type's highest allocation allowed, when it lowered the entitlement.</summary>
    public decimal? CeilingDays { get; init; }

    /// <summary>Months present in the hire year, when first-year pro-rating scaled the entitlement.</summary>
    public int? FirstYearMonthsPresent { get; init; }

    // ── the policy ──

    public bool HasPolicy { get; init; }
    public AccrualFrequency? Frequency { get; init; }
    public AccrualMode? Mode { get; init; }
    public int? MinServiceMonths { get; init; }
    public bool ProRateOnJoin { get; init; }
    public bool ProRateOnExit { get; init; }

    // ── the working ──

    public DateOnly? HiredOn { get; init; }
    public DateOnly? LeftOn { get; init; }

    /// <summary>The day accrual may begin: the hire date plus the policy's minimum service.</summary>
    public DateOnly? EligibleFrom { get; init; }

    /// <summary>The day the first period opens: the eligibility date or the year start (pro-rate on join).</summary>
    public DateOnly? WindowStart { get; init; }

    public int PeriodsPerYear { get; init; }

    /// <summary>
    /// Days per period. A derived rate is shown rounded; the running totals are the entitlement times
    /// the periods over the periods in a year, rounded once, so they are exact where the rate is not.
    /// </summary>
    public decimal RatePerPeriod { get; init; }

    /// <summary>The rate is the entitlement over the periods in a year (the policy's rate is 0).</summary>
    public bool RateIsDerived { get; init; }

    /// <summary>The completed periods, oldest first.</summary>
    public IReadOnlyList<LeaveAccrualPeriod> Periods { get; init; } = Array.Empty<LeaveAccrualPeriod>();

    /// <summary>The period under way after the last completed one, if it starts inside the year.</summary>
    public DateOnly? NextPeriodStart { get; init; }
    public DateOnly? NextPeriodEnd { get; init; }

    /// <summary>
    /// The next period runs past the year end, so the days from its start to the year end are never
    /// credited. Happens when the window opens mid-month (a joiner qualifying on the 12th).
    /// </summary>
    public bool TailNotCredited { get; init; }

    /// <summary>The days accrued as at <see cref="AsOf"/>.</summary>
    public decimal AccruedDays { get; init; }

    /// <summary>The accrual has reached the year's entitlement.</summary>
    public bool CapReached { get; init; }
}

/// <summary>
/// Single authority for "how many days is this employee entitled to / accrued-to-date" for a
/// leave type and year. Accrual is computed on read (no scheduled job): the engine derives the
/// accrued figure from the employee's service and the accrual policy whenever asked.
/// </summary>
public interface ILeaveEntitlementService
{
    /// <summary>
    /// Resolves the full annual entitlement. Precedence: effective-dated
    /// <c>LeaveCategoryAllocation</c> for the employee's staff level → <c>LeaveType.DefaultDaysPerYear</c>.
    /// </summary>
    Task<decimal> ResolveAnnualEntitlementAsync(Guid employeeId, Guid leaveTypeId, Guid? leaveSubTypeId, int year, CancellationToken ct = default);

    /// <summary>
    /// Days accrued as of <paramref name="asOf"/> (defaults to today), capped at the annual
    /// entitlement. Returns the full entitlement when no active accrual policy applies.
    /// </summary>
    Task<decimal> GetAccruedAsOfAsync(Guid employeeId, Guid leaveTypeId, Guid? leaveSubTypeId, int year, DateOnly? asOf = null, CancellationToken ct = default);

    /// <summary>
    /// Whether the employee has met the leave type's service gate (<c>MinServiceMonthsToAccess</c>)
    /// and may apply for it as of <paramref name="asOf"/> (defaults to today).
    /// </summary>
    Task<bool> IsAccessibleAsync(Guid employeeId, Guid leaveTypeId, DateOnly? asOf = null, CancellationToken ct = default);

    /// <summary>Convenience roll-up of entitlement, accrued-to-date and accessibility.</summary>
    Task<LeaveEntitlementSnapshot> GetSnapshotAsync(Guid employeeId, Guid leaveTypeId, Guid? leaveSubTypeId, int year, DateOnly? asOf = null, CancellationToken ct = default);

    /// <summary>
    /// The same snapshot for many employees at once, keyed by employee (round 5, lane C6). The leave
    /// type's rules are read once, and the caller supplies each employee's facts — so a report over
    /// every employee costs a handful of queries rather than several per person.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, LeaveEntitlementSnapshot>> GetSnapshotsAsync(
        IReadOnlyCollection<LeaveAccrualSubject> subjects, Guid leaveTypeId, int year, DateOnly? asOf = null, CancellationToken ct = default);

    /// <summary>
    /// <see cref="GetSnapshotsAsync"/> for a tenant, and a leave-year start month, that the caller
    /// names — for a sweep with no signed-in user (round 5, lane I).
    /// </summary>
    /// <remarks>
    /// ⚠ Every other read here takes its tenant, and its leave year, from the signed-in user. The
    /// nightly reminder host has none, so the September chase's first scheduled run on UAT threw
    /// "No tenant is associated with the current user" (2026-09-26) while every manual run — made by
    /// a signed-in admin — passed. The caller passes the tenant of the records it is sweeping and
    /// the start month from that tenant's own settings (<c>ICompanyHrPolicyProvider.GetForTenantAsync</c>),
    /// never <c>ILeaveYearContext</c>, whose scoped cache would hand a second tenant the
    /// first one's leave year.
    /// </remarks>
    Task<IReadOnlyDictionary<Guid, LeaveEntitlementSnapshot>> GetSnapshotsForTenantAsync(
        Guid tenantId, int leaveYearStartMonth, IReadOnlyCollection<LeaveAccrualSubject> subjects, Guid leaveTypeId, int year,
        DateOnly asOf, CancellationToken ct = default);

    /// <summary>
    /// How the accrual is worked out, step by step, as at <paramref name="asOf"/> (defaults to
    /// today) — the accrual statement (round 5, lane C2).
    /// </summary>
    Task<LeaveAccrualWorking> GetAccrualWorkingAsync(Guid employeeId, Guid leaveTypeId, int year, DateOnly? asOf = null, CancellationToken ct = default);
}
