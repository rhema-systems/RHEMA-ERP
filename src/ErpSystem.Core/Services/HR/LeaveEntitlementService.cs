using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Entities.HR.StaffLeave;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Common;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR;

/// <summary>
/// Computes leave entitlement and accrued-to-date figures on read. See
/// <see cref="ILeaveEntitlementService"/>. The accrual math is intentionally calendar-year based
/// and driven entirely by configuration (no hardcoded leave names): a leave type "behaves like
/// annual leave" purely because it is configured with a service gate and an accrual policy.
/// </summary>
public class LeaveEntitlementService : ILeaveEntitlementService
{
    private readonly IGenericRepository<Employee> _employeeRepository;
    private readonly IGenericRepository<LeaveType> _leaveTypeRepository;
    private readonly IGenericRepository<LeaveSubType> _leaveSubTypeRepository;
    private readonly IGenericRepository<LeaveCategoryAllocation> _allocationRepository;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IDateTimeProvider _clock;
    private readonly ILogger<LeaveEntitlementService> _logger;

    public LeaveEntitlementService(
        IGenericRepository<Employee> employeeRepository,
        IGenericRepository<LeaveType> leaveTypeRepository,
        IGenericRepository<LeaveSubType> leaveSubTypeRepository,
        IGenericRepository<LeaveCategoryAllocation> allocationRepository,
        ICurrentUserProvider currentUserProvider,
        IDateTimeProvider clock,
        ILogger<LeaveEntitlementService> logger)
    {
        _employeeRepository = employeeRepository;
        _leaveTypeRepository = leaveTypeRepository;
        _leaveSubTypeRepository = leaveSubTypeRepository;
        _allocationRepository = allocationRepository;
        _currentUserProvider = currentUserProvider;
        _clock = clock;
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
    private async Task<LeaveType> GetOwnedLeaveTypeAsync(Guid id)
    {
        var entity = await _leaveTypeRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Leave type '{id}' not found.");
        return entity;
    }

    public async Task<decimal> ResolveAnnualEntitlementAsync(
        Guid employeeId, Guid leaveTypeId, Guid? leaveSubTypeId, int year, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        var leaveType = await GetOwnedLeaveTypeAsync(leaveTypeId);

        // 1) Sub-type cap takes precedence when set.
        if (leaveSubTypeId.HasValue)
        {
            var subType = await _leaveSubTypeRepository.GetByIdAsync(leaveSubTypeId.Value);
            if (subType?.TenantId == tenantId && subType.MaxDaysAllowed is int cap)
                return ApplyCeiling(leaveType, cap);
        }

        // 2) Effective-dated allocation for the employee's staff level.
        var staffLevelId = await GetEmployeeStaffLevelIdAsync(employeeId, tenantId, ct);
        if (staffLevelId.HasValue)
        {
            var yearStart = new DateOnly(year, 1, 1);
            var yearEnd = new DateOnly(year, 12, 31);

            var allocation = await _allocationRepository
                .GetQueryable()
                .Where(a => a.TenantId == tenantId
                         && a.LeaveTypeId == leaveTypeId
                         && a.StaffLevelId == staffLevelId.Value
                         && a.LeaveSubTypeId == leaveSubTypeId
                         && a.EffectiveFrom <= yearEnd
                         && (a.EffectiveTo == null || a.EffectiveTo >= yearStart))
                .OrderByDescending(a => a.EffectiveFrom)
                .FirstOrDefaultAsync(ct);

            if (allocation != null)
                return ApplyCeiling(leaveType, allocation.AllocationDays);
        }

        // 3) Fall back to the leave type default.
        return ApplyCeiling(leaveType, leaveType.DefaultDaysPerYear);
    }

    /// <summary>
    /// Clamps a resolved entitlement to the leave type's absolute annual ceiling
    /// (<see cref="LeaveType.MaxDaysPerYear"/>) when one is configured (&gt; 0).
    /// </summary>
    private static decimal ApplyCeiling(LeaveType leaveType, decimal value)
        => leaveType.MaxDaysPerYear > 0 ? Math.Min(value, leaveType.MaxDaysPerYear) : value;

    public async Task<decimal> GetAccruedAsOfAsync(
        Guid employeeId, Guid leaveTypeId, Guid? leaveSubTypeId, int year, DateOnly? asOf = null, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        var annual = await ResolveAnnualEntitlementAsync(employeeId, leaveTypeId, leaveSubTypeId, year, ct);

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

        // No active accrual policy → the full entitlement is available immediately (legacy behavior).
        if (policy == null)
            return annual;

        var employee = await _employeeRepository.GetByIdAsync(employeeId);
        var isOwn = employee?.TenantId == tenantId;
        var dateEmployed = isOwn ? employee!.DateEmployed : null;
        var dateLeft = isOwn && employee!.TerminationDate is DateTime left
            ? DateOnly.FromDateTime(left)
            : (DateOnly?)null;

        var yearStart = new DateOnly(year, 1, 1);
        var yearEnd = new DateOnly(year, 12, 31);

        // Clamp the as-of date into the target year for within-year accrual.
        var effectiveAsOf = asOf ?? _clock.TodayUtc;
        if (effectiveAsOf > yearEnd) effectiveAsOf = yearEnd;

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
        if (policy.ProRateOnExit && dateLeft is DateOnly exit && exit < effectiveAsOf)
            effectiveAsOf = exit;

        if (effectiveAsOf < yearStart) return 0m; // year hasn't started yet, or they left before it

        // Accrual eligibility date = hire date + the policy's minimum-service months.
        var eligibilityDate = dateEmployed?.AddMonths(policy.MinServiceMonths ?? 0) ?? yearStart;
        if (effectiveAsOf < eligibilityDate)
            return 0m; // not yet accruing this leave type

        if (policy.Mode == AccrualMode.FullGrantOnEligibility)
            return annual;

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
            ? Max(yearStart, eligibilityDate)
            : yearStart;

        var periodsPerYear = PeriodsPerYear(policy.Frequency);
        var ratePerPeriod = policy.AccrualRate > 0 ? policy.AccrualRate : annual / periodsPerYear;

        var completedPeriods = CompletedPeriods(accrualStart, effectiveAsOf, policy.Frequency);
        var accrued = completedPeriods * ratePerPeriod;

        return Math.Min(accrued, annual);
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
        var tenantId = GetTenantId();
        var leaveType = await GetOwnedLeaveTypeAsync(leaveTypeId);

        var annual = await ResolveAnnualEntitlementAsync(employeeId, leaveTypeId, leaveSubTypeId, year, ct);
        var accrued = await GetAccruedAsOfAsync(employeeId, leaveTypeId, leaveSubTypeId, year, asOf, ct);

        var hasPolicy = await _leaveTypeRepository
            .GetQueryable()
            .Where(lt => lt.TenantId == tenantId && lt.Id == leaveTypeId)
            .SelectMany(lt => lt.AccrualPolicies)
            .AnyAsync(p => p.IsActive && p.Frequency != AccrualFrequency.None, ct);

        DateOnly? accessibleFrom = null;
        var minMonths = leaveType.MinServiceMonthsToAccess ?? 0;
        if (minMonths > 0)
        {
            var employee = await _employeeRepository.GetByIdAsync(employeeId);
            if (employee?.TenantId == tenantId && employee.DateEmployed is DateOnly hired)
                accessibleFrom = hired.AddMonths(minMonths);
        }

        return new LeaveEntitlementSnapshot
        {
            AnnualEntitledDays = annual,
            AccruedToDateDays = accrued,
            HasAccrualPolicy = hasPolicy,
            IsAccessible = accessibleFrom == null || (asOf ?? _clock.TodayUtc) >= accessibleFrom,
            AccessibleFrom = accessibleFrom
        };
    }

    // ===== helpers =====

    private async Task<Guid?> GetEmployeeStaffLevelIdAsync(Guid employeeId, Guid tenantId, CancellationToken ct)
    {
        return await _employeeRepository
            .GetQueryable()
            .Where(e => e.TenantId == tenantId && e.Id == employeeId)
            .Select(e => e.Position != null ? e.Position.StaffLevelId : null)
            .FirstOrDefaultAsync(ct);
    }

    private static int PeriodsPerYear(AccrualFrequency frequency) => frequency switch
    {
        AccrualFrequency.Monthly => 12,
        AccrualFrequency.PerPayPeriod => 12,
        AccrualFrequency.Quarterly => 4,
        AccrualFrequency.SemiAnnual => 2,
        AccrualFrequency.Annual => 1,
        _ => 1
    };

    /// <summary>
    /// Number of fully-completed accrual periods between <paramref name="start"/> and
    /// <paramref name="asOf"/>. A period is credited once its full duration has elapsed, so e.g.
    /// one month after the start of monthly accrual yields one period.
    /// </summary>
    private static int CompletedPeriods(DateOnly start, DateOnly asOf, AccrualFrequency frequency)
    {
        if (asOf <= start) return 0;

        int monthsElapsed = (asOf.Year - start.Year) * 12 + (asOf.Month - start.Month);
        if (asOf.Day < start.Day) monthsElapsed--;
        if (monthsElapsed < 0) monthsElapsed = 0;

        return frequency switch
        {
            AccrualFrequency.Monthly => monthsElapsed,
            AccrualFrequency.PerPayPeriod => monthsElapsed,
            AccrualFrequency.Quarterly => monthsElapsed / 3,
            AccrualFrequency.SemiAnnual => monthsElapsed / 6,
            AccrualFrequency.Annual => monthsElapsed / 12,
            _ => 0
        };
    }

    private static DateOnly Max(DateOnly a, DateOnly b) => a >= b ? a : b;
}
