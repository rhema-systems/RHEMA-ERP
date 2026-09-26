using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Entities.HR.StaffLeave;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Common;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Core.Interfaces.HR.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR;

/// <summary>
/// Computes leave entitlement and accrued-to-date figures on read. See
/// <see cref="ILeaveEntitlementService"/>. The accrual math is intentionally calendar-year based
/// and driven entirely by configuration (no hardcoded leave names): a leave type "behaves like
/// annual leave" purely because it is configured with a service gate and an accrual policy.
/// </summary>
/// <remarks>
/// <para><b>Round 5, lane C: one working, every reader.</b> The arithmetic lives in two pure methods,
/// <see cref="ResolveEntitlement"/> and <see cref="WorkOut"/>, over facts loaded once: the leave
/// type's rules for the year (<see cref="TypeRules"/>) and the employee's own
/// (<see cref="LeaveAccrualSubject"/>). The single-employee reads, the batch read behind the "leave
/// owed" report, and the accrual statement all go through them — so a statement's lines add up to
/// exactly the figure the create check enforces, and the report cannot drift from either.</para>
///
/// <para>Before this, the entitlement was resolved twice per snapshot and the accrual in a third
/// place, each with its own employee lookup — about ten queries for one balance row. Now it is four,
/// and a report over every employee is five in total.</para>
/// </remarks>
public class LeaveEntitlementService : ILeaveEntitlementService
{
    private readonly IGenericRepository<Employee> _employeeRepository;
    private readonly IGenericRepository<LeaveType> _leaveTypeRepository;
    private readonly IGenericRepository<LeaveCategoryAllocation> _allocationRepository;
    private readonly IGenericRepository<StaffLevel> _staffLevelRepository;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IDateTimeProvider _clock;
    private readonly ILeaveYearContext _leaveYear;
    private readonly ILogger<LeaveEntitlementService> _logger;

    public LeaveEntitlementService(
        IGenericRepository<Employee> employeeRepository,
        IGenericRepository<LeaveType> leaveTypeRepository,
        IGenericRepository<LeaveCategoryAllocation> allocationRepository,
        IGenericRepository<StaffLevel> staffLevelRepository,
        ICurrentUserProvider currentUserProvider,
        IDateTimeProvider clock,
        ILeaveYearContext leaveYear,
        ILogger<LeaveEntitlementService> logger)
    {
        _employeeRepository = employeeRepository;
        _leaveTypeRepository = leaveTypeRepository;
        _allocationRepository = allocationRepository;
        _staffLevelRepository = staffLevelRepository;
        _currentUserProvider = currentUserProvider;
        _clock = clock;
        _leaveYear = leaveYear;
        _logger = logger;
    }

    // The ApplicationDbContext is registered without a tenant, so its global tenant query-filter and
    // TenantId auto-stamp are inert. Following the RHEMA convention, this service scopes reads/writes to
    // the current tenant explicitly.
    private Guid GetTenantId()
    {
        var tenantId = _currentUserProvider.TenantId;
        if (tenantId == Guid.Empty)
            throw new InvalidOperationException("No tenant is associated with the current user.");
        return tenantId;
    }

    // A leave type owned by another tenant is reported as missing rather than forbidden, so the endpoints do
    // not confirm that the id exists elsewhere.
    private Task<LeaveType> GetOwnedLeaveTypeAsync(Guid id) => GetOwnedLeaveTypeAsync(id, GetTenantId());

    private async Task<LeaveType> GetOwnedLeaveTypeAsync(Guid id, Guid tenantId)
    {
        var entity = await _leaveTypeRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != tenantId)
            throw new ArgumentException($"Leave type '{id}' not found.");
        return entity;
    }

    // ===== the public reads =====

    public async Task<decimal> ResolveAnnualEntitlementAsync(
        Guid employeeId, Guid leaveTypeId, Guid? leaveSubTypeId, int year, CancellationToken ct = default)
    {
        // ⚠ The SUB-TYPE is deliberately not read (round 5, lane N2). A balance is kept per type, so
        // every sub-type draws on the type's one pot, and a sub-type's cap limits that sub-type
        // INSIDE the pot — enforced per request by LeaveService.EnsureSubTypeCapAsync. It used to
        // replace the whole entitlement here, so a request carrying a sub-type was measured against
        // the cap instead of the pot; and allocations were matched on the request's sub-type, so a
        // staff-level allocation missed every request that carried one and fell to the default.
        _ = leaveSubTypeId;

        var rules = await LoadRulesAsync(leaveTypeId, year, ct);
        return ResolveEntitlement(rules, await LoadSubjectAsync(employeeId, ct)).Days;
    }

    public async Task<decimal> GetAccruedAsOfAsync(
        Guid employeeId, Guid leaveTypeId, Guid? leaveSubTypeId, int year, DateOnly? asOf = null, CancellationToken ct = default)
    {
        _ = leaveSubTypeId; // the pot is the type's — see ResolveAnnualEntitlementAsync
        var rules = await LoadRulesAsync(leaveTypeId, year, ct);
        var subject = await LoadSubjectAsync(employeeId, ct);
        return WorkOut(rules, subject, ResolveEntitlement(rules, subject), asOf ?? _clock.TodayUtc).AccruedDays;
    }

    public async Task<bool> IsAccessibleAsync(
        Guid employeeId, Guid leaveTypeId, DateOnly? asOf = null, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        var leaveType = await GetOwnedLeaveTypeAsync(leaveTypeId);

        var minMonths = leaveType.MinServiceMonthsToAccess ?? 0;
        if (minMonths <= 0)
            return true;

        var employee = await _employeeRepository.GetByIdAsync(employeeId);
        // No hire date on record → don't block; treat as accessible.
        if (employee?.TenantId != tenantId || employee.DateEmployed is not DateOnly hired)
            return true;

        var accessibleFrom = hired.AddMonths(minMonths);
        return (asOf ?? _clock.TodayUtc) >= accessibleFrom;
    }

    public async Task<LeaveEntitlementSnapshot> GetSnapshotAsync(
        Guid employeeId, Guid leaveTypeId, Guid? leaveSubTypeId, int year, DateOnly? asOf = null, CancellationToken ct = default)
    {
        _ = leaveSubTypeId; // the pot is the type's — see ResolveAnnualEntitlementAsync
        var rules = await LoadRulesAsync(leaveTypeId, year, ct);
        return Snapshot(rules, await LoadSubjectAsync(employeeId, ct), asOf ?? _clock.TodayUtc);
    }

    public async Task<IReadOnlyDictionary<Guid, LeaveEntitlementSnapshot>> GetSnapshotsAsync(
        IReadOnlyCollection<LeaveAccrualSubject> subjects, Guid leaveTypeId, int year, DateOnly? asOf = null, CancellationToken ct = default)
        => SnapshotsOf(await LoadRulesAsync(leaveTypeId, year, ct), subjects, asOf ?? _clock.TodayUtc);

    public async Task<IReadOnlyDictionary<Guid, LeaveEntitlementSnapshot>> GetSnapshotsForTenantAsync(
        Guid tenantId, int leaveYearStartMonth, IReadOnlyCollection<LeaveAccrualSubject> subjects, Guid leaveTypeId, int year,
        DateOnly asOf, CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("A tenant is required.", nameof(tenantId));

        // Out of range degrades to January, as LeaveYearContext does for the signed-in path.
        var startMonth = leaveYearStartMonth is >= 1 and <= 12 ? leaveYearStartMonth : LeaveYear.CalendarStartMonth;
        return SnapshotsOf(await LoadRulesAsync(tenantId, startMonth, leaveTypeId, year, ct), subjects, asOf);
    }

    private static Dictionary<Guid, LeaveEntitlementSnapshot> SnapshotsOf(
        TypeRules rules, IReadOnlyCollection<LeaveAccrualSubject> subjects, DateOnly when)
    {
        var snapshots = new Dictionary<Guid, LeaveEntitlementSnapshot>();
        foreach (var subject in subjects)
            snapshots[subject.EmployeeId] = Snapshot(rules, subject, when);
        return snapshots;
    }

    public async Task<LeaveAccrualWorking> GetAccrualWorkingAsync(
        Guid employeeId, Guid leaveTypeId, int year, DateOnly? asOf = null, CancellationToken ct = default)
    {
        var rules = await LoadRulesAsync(leaveTypeId, year, ct);
        var subject = await LoadSubjectAsync(employeeId, ct);
        var working = WorkOut(rules, subject, ResolveEntitlement(rules, subject), asOf ?? _clock.TodayUtc);

        if (working.StaffLevelId is not Guid levelId)
            return working;

        // The grade's NAME is for the statement alone, so it is looked up here rather than joined into
        // every accrual read — and looked up on its own, so a retired staff level still names itself.
        var tenantId = GetTenantId();
        var levelName = await _staffLevelRepository
            .GetQueryableIncludingDeleted(l => l.Id == levelId && l.TenantId == tenantId)
            .Select(l => l.Name)
            .FirstOrDefaultAsync(ct);
        return Named(working, levelName);
    }

    // ===== the facts =====

    /// <summary>A leave type's accrual rules for one leave year, read once per call.</summary>
    private sealed class TypeRules
    {
        public required LeaveType Type { get; init; }
        public required int Year { get; init; }
        public required int StartMonth { get; init; }
        public required DateOnly YearStart { get; init; }
        public required DateOnly YearEnd { get; init; }
        public LeaveAccrualPolicy? Policy { get; init; }

        /// <summary>The type's staff-level allocations in force at some point of the year.</summary>
        public required IReadOnlyList<LeaveCategoryAllocation> Allocations { get; init; }
    }

    private async Task<TypeRules> LoadRulesAsync(Guid leaveTypeId, int year, CancellationToken ct)
    {
        var tenantId = GetTenantId();

        // ⚠ The tenant's leave year, resolved ONCE for the call. Every arm of the precedence, the
        // pro-rating helper and the accrual window all need the same answer, and the context caches
        // it for the request in any case (entitlement plan C1).
        return await LoadRulesAsync(tenantId, await _leaveYear.StartMonthAsync(ct), leaveTypeId, year, ct);
    }

    /// <summary>
    /// The rules for a tenant and leave-year start month the caller names — the signed-in path above,
    /// and the nightly reminder sweep's (round 5, lane I), which has no user to read either from.
    /// </summary>
    private async Task<TypeRules> LoadRulesAsync(Guid tenantId, int startMonth, Guid leaveTypeId, int year, CancellationToken ct)
    {
        var leaveType = await GetOwnedLeaveTypeAsync(leaveTypeId, tenantId);
        var yearStart = LeaveYear.StartOf(year, startMonth);
        var yearEnd = LeaveYear.EndOf(year, startMonth);

        // ⚠ Entitlement plan A2. This used to be a bare FirstOrDefault over the active policies,
        // with no ordering — so a leave type carrying two of them accrued at whichever rate the
        // database happened to return first, and the same balance could read differently between
        // two calls. `LeaveTypeService` now refuses a second active policy at the door; this
        // ordering is for the rows that predate that guard, so at least the wrong answer is the
        // SAME wrong answer every time and a repair pass can be reasoned about.
        var policy = await _leaveTypeRepository
            .GetQueryable()
            .Where(lt => lt.TenantId == tenantId && lt.Id == leaveTypeId)
            .SelectMany(lt => lt.AccrualPolicies)
            .Where(p => p.IsActive && p.Frequency != AccrualFrequency.None)
            .OrderBy(p => p.CreatedAt)
            .ThenBy(p => p.Id)
            .FirstOrDefaultAsync(ct);

        // Effective-dated allocations for the whole type (never a sub-type's — lane N2), every staff
        // level at once: a handful of rows per type, and the same rows whether one employee is asked
        // about or two thousand. Which one applies is picked in memory, per staff level.
        var allocations = await _allocationRepository
            .GetQueryable()
            .Where(a => a.TenantId == tenantId
                     && a.LeaveTypeId == leaveTypeId
                     && a.LeaveSubTypeId == null
                     && a.EffectiveFrom <= yearEnd
                     && (a.EffectiveTo == null || a.EffectiveTo >= yearStart))
            .ToListAsync(ct);

        return new TypeRules
        {
            Type = leaveType,
            Year = year,
            StartMonth = startMonth,
            YearStart = yearStart,
            YearEnd = yearEnd,
            Policy = policy,
            Allocations = allocations,
        };
    }

    private async Task<LeaveAccrualSubject> LoadSubjectAsync(Guid employeeId, CancellationToken ct)
    {
        var tenantId = GetTenantId();
        var facts = await _employeeRepository
            .GetQueryable()
            .Where(e => e.TenantId == tenantId && e.Id == employeeId)
            .Select(e => new
            {
                e.DateEmployed,
                e.TerminationDate,
                StaffLevelId = e.Position != null ? e.Position.StaffLevelId : null,
            })
            .FirstOrDefaultAsync(ct);

        // Another tenant's employee, or nobody: no hire date, no last day and no staff level — which
        // is what each of the three older lookups answered on its own when it came back empty.
        return new LeaveAccrualSubject(
            employeeId,
            facts?.DateEmployed,
            facts?.TerminationDate is DateTime left ? DateOnly.FromDateTime(left) : null,
            facts?.StaffLevelId);
    }

    // ===== the arithmetic =====

    /// <summary>A resolved annual entitlement, and where it came from.</summary>
    private sealed record Entitlement(
        decimal Days,
        LeaveEntitlementSource Source,
        decimal BaseDays,
        LeaveCategoryAllocation? Allocation,
        decimal? CeilingDays,
        int? FirstYearMonthsPresent);

    private static Entitlement ResolveEntitlement(TypeRules rules, LeaveAccrualSubject subject)
    {
        // 1) Effective-dated allocation for the employee's staff level, for the whole type: the one
        //    that took effect last, among those in force at some point of the year.
        var allocation = subject.StaffLevelId is Guid levelId
            ? rules.Allocations
                .Where(a => a.StaffLevelId == levelId)
                .OrderByDescending(a => a.EffectiveFrom)
                .ThenBy(a => a.Id)
                .FirstOrDefault()
            : null;

        // 2) Otherwise the leave type default.
        decimal baseDays = allocation?.AllocationDays ?? rules.Type.DefaultDaysPerYear;

        // ⚠ Read once, applied to whichever arm answered — a joiner's first year is scaled the same
        // way whether the figure came from an allocation or the default.
        var ceilinged = ApplyCeiling(rules.Type, baseDays);
        var monthsPresent = FirstYearMonthsPresent(rules.Type, subject.HiredOn, rules.Year, rules.StartMonth);
        var days = monthsPresent is int months
            ? Math.Round(ceilinged * months / 12m, 2, MidpointRounding.AwayFromZero)
            : ceilinged;

        return new Entitlement(
            days,
            allocation != null ? LeaveEntitlementSource.StaffLevelAllocation : LeaveEntitlementSource.LeaveTypeDefault,
            baseDays,
            allocation,
            ceilinged < baseDays ? ceilinged : null,
            monthsPresent);
    }

    /// <summary>
    /// The months of <paramref name="year"/> the employee was here for, when the leave type scales a
    /// joiner's first year (entitlement plan B3, decision D-4); otherwise <c>null</c>.
    /// </summary>
    /// <remarks>
    /// <para>⚠ <b>Only in the year they were hired.</b> In every later year they were present
    /// throughout and there is nothing to scale.</para>
    ///
    /// <para><b>Whole months from the hire month to the end of the leave year, over twelve.</b>
    /// Somebody who starts on the 1st and somebody who starts on the 28th of the same month are
    /// treated alike, which is the ordinary reading of "you joined in October, so you get a quarter of
    /// the year" and avoids inventing a day-level rule nobody asked for.</para>
    ///
    /// <para>⚠ <b>Counted in months of the LEAVE year</b> (round 5, lane C4). This was
    /// <c>12 - hired.Month + 1</c> — months to December — which is right only for a January start:
    /// under an April start somebody hired in February joins in month 11 of their leave year and is
    /// present for two months, not eleven.</para>
    ///
    /// <para>⚠ <b>It cannot combine with incremental accrual</b> — <c>LeaveTypeService</c> refuses
    /// that pairing at the door. If it ever reached here, the same months would be deducted three
    /// times: once by the accrual window opening at the hire date, once by this scaling, and once
    /// more through the derived per-period rate, which is <i>this scaled figure</i> divided by the
    /// periods in a year.</para>
    /// </remarks>
    private static int? FirstYearMonthsPresent(LeaveType leaveType, DateOnly? dateEmployed, int year, int startMonth)
    {
        if (!leaveType.ProRateFirstYearEntitlement) return null;
        // ⚠ The leave year the hire date falls in, not its calendar year. Under an April start
        // somebody hired in February 2026 joined during leave year 2025, and comparing
        // calendar years would have silently skipped their pro-rating.
        if (dateEmployed is not DateOnly hired || LeaveYear.For(hired, startMonth) != year) return null;

        return 12 - LeaveYear.MonthOf(hired, startMonth) + 1;
    }

    /// <summary>
    /// Clamps a resolved entitlement to the annual leave type's highest allocation allowed
    /// (<see cref="LeaveType.MaxDaysPerYear"/>) when one is configured (&gt; 0).
    /// </summary>
    /// <remarks>
    /// ⚠ <b>Annual leave only</b> (round 5, lane N2). For every other kind the days per year ARE the
    /// limit, and a ceiling only ever lowered it: a type with a default of 0 and a maximum of 90 —
    /// the demo's unpaid and injury leave — could never be booked at all. On annual leave it is
    /// what its label now says, the highest a staff-level allocation may grant.
    /// </remarks>
    private static decimal ApplyCeiling(LeaveType leaveType, decimal value)
        => leaveType.Category == LeaveTypeCategory.Annual && leaveType.MaxDaysPerYear > 0
            ? Math.Min(value, leaveType.MaxDaysPerYear)
            : value;

    private static LeaveEntitlementSnapshot Snapshot(TypeRules rules, LeaveAccrualSubject subject, DateOnly asOf)
    {
        var entitlement = ResolveEntitlement(rules, subject);
        var working = WorkOut(rules, subject, entitlement, asOf);

        DateOnly? accessibleFrom = null;
        var minMonths = rules.Type.MinServiceMonthsToAccess ?? 0;
        if (minMonths > 0 && subject.HiredOn is DateOnly hired)
            accessibleFrom = hired.AddMonths(minMonths);

        return new LeaveEntitlementSnapshot
        {
            AnnualEntitledDays = entitlement.Days,
            AccruedToDateDays = working.AccruedDays,
            AccruedAsOf = working.HasPolicy ? working.AsOf : null,
            HasAccrualPolicy = working.HasPolicy,
            IsAccessible = accessibleFrom == null || asOf >= accessibleFrom,
            AccessibleFrom = accessibleFrom
        };
    }

    /// <summary>
    /// Works out the days accrued as at <paramref name="requestedAsOf"/>, and how.
    /// </summary>
    private static LeaveAccrualWorking WorkOut(
        TypeRules rules, LeaveAccrualSubject subject, Entitlement entitlement, DateOnly requestedAsOf)
    {
        var annual = entitlement.Days;
        var policy = rules.Policy;

        LeaveAccrualWorking Result(
            LeaveAccrualState state, decimal accrued, DateOnly workedTo, LeaveAccrualAsOfLimit stoppedBy,
            DateOnly? eligibleFrom = null, DateOnly? windowStart = null, int perYear = 0,
            decimal ratePerPeriod = 0m, bool rateIsDerived = false,
            IReadOnlyList<LeaveAccrualPeriod>? lines = null,
            DateOnly? nextFrom = null, DateOnly? nextTo = null, bool tailNotCredited = false) => new()
        {
            Year = rules.Year,
            YearStart = rules.YearStart,
            YearEnd = rules.YearEnd,
            RequestedAsOf = requestedAsOf,
            AsOf = workedTo,
            AsOfLimit = stoppedBy,
            State = state,
            AnnualEntitledDays = annual,
            EntitlementSource = entitlement.Source,
            EntitlementBaseDays = entitlement.BaseDays,
            StaffLevelId = entitlement.Allocation?.StaffLevelId,
            AllocationEffectiveFrom = entitlement.Allocation?.EffectiveFrom,
            CeilingDays = entitlement.CeilingDays,
            FirstYearMonthsPresent = entitlement.FirstYearMonthsPresent,
            HasPolicy = policy != null,
            Frequency = policy?.Frequency,
            Mode = policy?.Mode,
            MinServiceMonths = policy?.MinServiceMonths,
            ProRateOnJoin = policy?.ProRateOnJoin ?? false,
            ProRateOnExit = policy?.ProRateOnExit ?? false,
            HiredOn = subject.HiredOn,
            LeftOn = subject.LeftOn,
            EligibleFrom = eligibleFrom,
            WindowStart = windowStart,
            PeriodsPerYear = perYear,
            RatePerPeriod = ratePerPeriod,
            RateIsDerived = rateIsDerived,
            Periods = lines ?? Array.Empty<LeaveAccrualPeriod>(),
            NextPeriodStart = nextFrom,
            NextPeriodEnd = nextTo,
            TailNotCredited = tailNotCredited,
            AccruedDays = accrued,
            CapReached = annual > 0 && accrued >= annual,
        };

        // No active accrual policy → the full entitlement is available immediately (legacy behavior).
        if (policy == null)
            return Result(LeaveAccrualState.NoPolicy, annual, requestedAsOf, LeaveAccrualAsOfLimit.None);

        // Clamp the as-of date into the target year for within-year accrual.
        var asOf = requestedAsOf;
        var limit = LeaveAccrualAsOfLimit.None;
        if (asOf > rules.YearEnd)
        {
            asOf = rules.YearEnd;
            limit = LeaveAccrualAsOfLimit.YearEnd;
        }

        // Pro-rate on exit: a leaver stops accruing on their last day, so the clock stops there
        // rather than running to today or to year end. This is the mirror of ProRateOnJoin, which
        // moves the START of the accrual window to the hire date — the two switches are a pair, and
        // until now only one of them was read by anything (closure plan L-29).
        //
        // ⚠ It applies to incremental accrual only, exactly as ProRateOnJoin does. A policy set to
        // FullGrantOnEligibility grants the whole year the moment eligibility is reached, and a
        // "full grant" that is then reduced is no longer a full grant — so the two settings do not
        // combine. If TDC wants a leaver's full grant scaled down, that is a different setting and
        // it needs saying (decision D-6's neighbour; recorded in the closure ledger).
        if (policy.ProRateOnExit && subject.LeftOn is DateOnly exit && exit < asOf)
        {
            asOf = exit;
            limit = LeaveAccrualAsOfLimit.LastDayOfService;
        }

        if (asOf < rules.YearStart) // year hasn't started yet, or they left before it
            return Result(LeaveAccrualState.YearNotStarted, 0m, asOf, limit);

        // Accrual eligibility date = hire date + the policy's minimum-service months.
        var eligibilityDate = subject.HiredOn?.AddMonths(policy.MinServiceMonths ?? 0) ?? rules.YearStart;
        if (asOf < eligibilityDate) // not yet accruing this leave type
            return Result(LeaveAccrualState.NotYetEligible, 0m, asOf, limit, eligibleFrom: eligibilityDate);

        if (policy.Mode == AccrualMode.FullGrantOnEligibility)
            return Result(LeaveAccrualState.FullGrant, annual, asOf, limit, eligibleFrom: eligibilityDate);

        // AccrueIncrementally: credit AccrualRate per completed period from the accrual start.
        //
        // ⚠ Entitlement plan B1 / decision D-1. `ProRateOnJoin` decides where the accrual WINDOW
        // opens, and until this it decided nothing at all: the guard read
        // `hired > Max(yearStart, eligibilityDate)`, and since `eligibilityDate` is `hired` plus a
        // non-negative number of months, that compares the hire date with something that is never
        // earlier than the hire date. Unsatisfiable, for every employee and every policy. The
        // pro-rating happened regardless — the eligibility date had already anchored the window to
        // the hire date — so the switch could not be turned OFF, which is the half that was missing.
        //
        //   ON  — the window opens when the employee became eligible, so a mid-year joiner earns
        //         only the part of the year they were here for. Identical to the behaviour before
        //         this change: ⚠ deliberately so, because every existing row's value was arbitrary
        //         while the field was inert, and an accrual figure must not move under a tenant
        //         that never chose anything.
        //
        //   OFF — the window opens with the LEAVE YEAR. Once somebody qualifies at all, they accrue
        //         on the company's calendar like everybody else. This is the reading the field name
        //         promises and the one no client could previously have.
        //
        // ⚠ The service gate is a separate thing and still binds either way: the early return above
        // gives 0 before `eligibilityDate`, so OFF cannot credit somebody for a year in which they
        // were never eligible. What it does is stop docking them for the months before they arrived.
        var accrualStart = policy.ProRateOnJoin
            ? Max(rules.YearStart, eligibilityDate)
            : rules.YearStart;

        var periodsPerYear = PeriodsPerYear(policy.Frequency);
        var monthsPerPeriod = 12 / periodsPerYear;

        // ⚠ Entitlement plan B4, and it is the least discoverable useful behaviour in this service.
        //
        // A rate of ZERO does not mean "accrues nothing". It means the rate is DERIVED from this
        // employee's own annual entitlement — which came from the staff-level allocation — so one
        // policy makes a junior on 15 days accrue 1.25 a month and a manager on 30 accrue 2.5.
        // **That is how an accrual rate varies by staff level**, and nothing said so anywhere until
        // the tab was made to explain it.
        //
        // ⚠ Setting an explicit rate DEFEATS it, and the failure is quiet in both directions:
        // somebody entitled to less than the rate accumulates to reaches their cap early (the
        // cap below), and somebody entitled to more never reaches their full entitlement at
        // all. The demo seed carried a flat 1.75 against allocations of 15, 21 and 30 for exactly
        // that reason — it predated the allocations.
        var fixedRate = policy.AccrualRate;
        var derived = fixedRate <= 0;

        // ⚠ Round 5, lane C1: a derived total is the entitlement times the periods over the periods
        // in a year, rounded ONCE to the hundredth — never the periods times a rounded or repeating
        // rate. With December now credited, twelve periods of 10/12 would have summed to
        // 9.9999999999999999999999999996 and refused a ten-day request on the last day of the year;
        // 10 × 12 / 12 is exactly 10. The cap is the entitlement, as before.
        decimal CreditedAfter(int count) => Math.Min(
            annual,
            Math.Round(derived ? annual * count / periodsPerYear : fixedRate * count,
                       2, MidpointRounding.AwayFromZero));

        bool Overran(int count) =>
            (derived ? annual * count / periodsPerYear : fixedRate * count) > annual;

        // ⚠ Round 5, lane C1 — THE counting fix. A period is complete on its LAST DAY. It used to be
        // credited only once the day after it had arrived (whole months elapsed), and the as-of date
        // is clamped to the year end — so the twelfth month of a year was never credited inside
        // that year: monthly accrual topped out at 11/12 (22 of 24 days on 31 December), quarterly
        // at 3/4, half-yearly at 1/2, and incremental "Annual" at nothing at all. A leaver whose
        // last day ended a month lost that month the same way.
        //
        // Each period is measured from the window start (never chained from the previous period's
        // end), so a window opening on the 31st does not drift through the short months.
        var periods = new List<LeaveAccrualPeriod>();
        DateOnly? nextStart = null, nextEnd = null;
        for (var k = 1; k <= periodsPerYear + 1; k++)
        {
            var periodStart = accrualStart.AddMonths((k - 1) * monthsPerPeriod);
            var periodEnd = accrualStart.AddMonths(k * monthsPerPeriod).AddDays(-1);

            if (periodEnd > asOf)
            {
                if (periodStart <= rules.YearEnd)
                {
                    nextStart = periodStart;
                    nextEnd = periodEnd;
                }
                break;
            }

            var runningTotal = CreditedAfter(k);
            periods.Add(new LeaveAccrualPeriod
            {
                Start = periodStart,
                End = periodEnd,
                Days = runningTotal - CreditedAfter(k - 1),
                RunningTotal = runningTotal,
                Capped = Overran(k),
            });
        }

        var displayRate = derived
            ? Math.Round(annual / periodsPerYear, 2, MidpointRounding.AwayFromZero)
            : fixedRate;

        return Result(
            LeaveAccrualState.Accruing,
            periods.Count == 0 ? 0m : periods[^1].RunningTotal,
            asOf, limit,
            eligibleFrom: eligibilityDate,
            windowStart: accrualStart,
            perYear: periodsPerYear,
            ratePerPeriod: displayRate,
            rateIsDerived: derived,
            lines: periods,
            nextFrom: nextStart,
            nextTo: nextEnd,
            // ⚠ A window opening mid-month (a joiner qualifying on the 12th) leaves a stretch after
            // the last whole period that ends inside the year; that period runs past the year end and
            // is never credited. Whole periods only is the existing rule, and the round 5 explainer's
            // worked example (18 days for nine months) states it — the statement says so rather than
            // leaving somebody to find the missing days.
            tailNotCredited: nextEnd is DateOnly end && end > rules.YearEnd);
    }

    /// <summary>The same working with its staff level's name filled in.</summary>
    private static LeaveAccrualWorking Named(LeaveAccrualWorking w, string? staffLevelName) => new()
    {
        Year = w.Year, YearStart = w.YearStart, YearEnd = w.YearEnd,
        RequestedAsOf = w.RequestedAsOf, AsOf = w.AsOf, AsOfLimit = w.AsOfLimit, State = w.State,
        AnnualEntitledDays = w.AnnualEntitledDays, EntitlementSource = w.EntitlementSource,
        EntitlementBaseDays = w.EntitlementBaseDays, StaffLevelId = w.StaffLevelId,
        StaffLevelName = staffLevelName, AllocationEffectiveFrom = w.AllocationEffectiveFrom,
        CeilingDays = w.CeilingDays, FirstYearMonthsPresent = w.FirstYearMonthsPresent,
        HasPolicy = w.HasPolicy, Frequency = w.Frequency, Mode = w.Mode,
        MinServiceMonths = w.MinServiceMonths, ProRateOnJoin = w.ProRateOnJoin, ProRateOnExit = w.ProRateOnExit,
        HiredOn = w.HiredOn, LeftOn = w.LeftOn, EligibleFrom = w.EligibleFrom, WindowStart = w.WindowStart,
        PeriodsPerYear = w.PeriodsPerYear, RatePerPeriod = w.RatePerPeriod, RateIsDerived = w.RateIsDerived,
        Periods = w.Periods, NextPeriodStart = w.NextPeriodStart, NextPeriodEnd = w.NextPeriodEnd,
        TailNotCredited = w.TailNotCredited, AccruedDays = w.AccruedDays, CapReached = w.CapReached,
    };

    // ===== helpers =====

    /// <summary>
    /// How many periods a leave year holds. <c>PerPayPeriod</c> is Monthly (retired from the picker,
    /// entitlement plan B5), and <c>Annual</c> is one period ending on the last day of the year —
    /// retired for incremental accrual by round 5 lane N2, but rows carrying it still work out.
    /// </summary>
    private static int PeriodsPerYear(AccrualFrequency frequency) => frequency switch
    {
        AccrualFrequency.Monthly => 12,
        AccrualFrequency.PerPayPeriod => 12,
        AccrualFrequency.Quarterly => 4,
        AccrualFrequency.SemiAnnual => 2,
        AccrualFrequency.Annual => 1,
        _ => 1
    };

    private static DateOnly Max(DateOnly a, DateOnly b) => a >= b ? a : b;
}
