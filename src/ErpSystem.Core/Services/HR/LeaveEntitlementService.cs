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
    private readonly IDateTimeProvider _clock;
    private readonly ILogger<LeaveEntitlementService> _logger;

    public LeaveEntitlementService(
        IGenericRepository<Employee> employeeRepository,
        IGenericRepository<LeaveType> leaveTypeRepository,
        IGenericRepository<LeaveSubType> leaveSubTypeRepository,
        IGenericRepository<LeaveCategoryAllocation> allocationRepository,
        IDateTimeProvider clock,
        ILogger<LeaveEntitlementService> logger)
    {
        _employeeRepository = employeeRepository;
        _leaveTypeRepository = leaveTypeRepository;
        _leaveSubTypeRepository = leaveSubTypeRepository;
        _allocationRepository = allocationRepository;
        _clock = clock;
        _logger = logger;
    }

    public async Task<decimal> ResolveAnnualEntitlementAsync(
        Guid employeeId, Guid leaveTypeId, Guid? leaveSubTypeId, int year, CancellationToken ct = default)
    {
        var leaveType = await _leaveTypeRepository.GetByIdAsync(leaveTypeId)
            ?? throw new ArgumentException($"Leave type '{leaveTypeId}' not found.");

        // 1) Sub-type cap takes precedence when set.
        if (leaveSubTypeId.HasValue)
        {
            var subType = await _leaveSubTypeRepository.GetByIdAsync(leaveSubTypeId.Value);
            if (subType?.MaxDaysAllowed is int cap)
                return ApplyCeiling(leaveType, cap);
        }

        // 2) Effective-dated allocation for the employee's staff level.
        var staffLevelId = await GetEmployeeStaffLevelIdAsync(employeeId, ct);
        if (staffLevelId.HasValue)
        {
            var yearStart = new DateOnly(year, 1, 1);
            var yearEnd = new DateOnly(year, 12, 31);

            var allocation = await _allocationRepository
                .GetQueryable()
                .Where(a => a.LeaveTypeId == leaveTypeId
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
        var annual = await ResolveAnnualEntitlementAsync(employeeId, leaveTypeId, leaveSubTypeId, year, ct);

        var policy = await _leaveTypeRepository
            .GetQueryable()
            .Where(lt => lt.Id == leaveTypeId)
            .SelectMany(lt => lt.AccrualPolicies)
            .Where(p => p.IsActive && p.Frequency != AccrualFrequency.None)
            .FirstOrDefaultAsync(ct);

        // No active accrual policy → the full entitlement is available immediately (legacy behavior).
        if (policy == null)
            return annual;

        var employee = await _employeeRepository.GetByIdAsync(employeeId);
        var dateEmployed = employee?.DateEmployed;

        var yearStart = new DateOnly(year, 1, 1);
        var yearEnd = new DateOnly(year, 12, 31);

        // Clamp the as-of date into the target year for within-year accrual.
        var effectiveAsOf = asOf ?? _clock.TodayUtc;
        if (effectiveAsOf > yearEnd) effectiveAsOf = yearEnd;
        if (effectiveAsOf < yearStart) return 0m; // year hasn't started yet

        // Accrual eligibility date = hire date + the policy's minimum-service months.
        var eligibilityDate = dateEmployed?.AddMonths(policy.MinServiceMonths ?? 0) ?? yearStart;
        if (effectiveAsOf < eligibilityDate)
            return 0m; // not yet accruing this leave type

        if (policy.Mode == AccrualMode.FullGrantOnEligibility)
            return annual;

        // AccrueIncrementally: credit AccrualRate per completed period from the accrual start.
        var accrualStart = Max(yearStart, eligibilityDate);
        if (policy.ProRateOnJoin && dateEmployed is DateOnly hired && hired > accrualStart && hired <= yearEnd)
            accrualStart = hired;

        var periodsPerYear = PeriodsPerYear(policy.Frequency);
        var ratePerPeriod = policy.AccrualRate > 0 ? policy.AccrualRate : annual / periodsPerYear;

        var completedPeriods = CompletedPeriods(accrualStart, effectiveAsOf, policy.Frequency);
        var accrued = completedPeriods * ratePerPeriod;

        return Math.Min(accrued, annual);
    }

    public async Task<bool> IsAccessibleAsync(
        Guid employeeId, Guid leaveTypeId, DateOnly? asOf = null, CancellationToken ct = default)
    {
        var leaveType = await _leaveTypeRepository.GetByIdAsync(leaveTypeId)
            ?? throw new ArgumentException($"Leave type '{leaveTypeId}' not found.");

        var minMonths = leaveType.MinServiceMonthsToAccess ?? 0;
        if (minMonths <= 0)
            return true;

        var employee = await _employeeRepository.GetByIdAsync(employeeId);
        // No hire date on record → don't block; treat as accessible.
        if (employee?.DateEmployed is not DateOnly hired)
            return true;

        var accessibleFrom = hired.AddMonths(minMonths);
        return (asOf ?? _clock.TodayUtc) >= accessibleFrom;
    }

    public async Task<LeaveEntitlementSnapshot> GetSnapshotAsync(
        Guid employeeId, Guid leaveTypeId, Guid? leaveSubTypeId, int year, DateOnly? asOf = null, CancellationToken ct = default)
    {
        var leaveType = await _leaveTypeRepository.GetByIdAsync(leaveTypeId)
            ?? throw new ArgumentException($"Leave type '{leaveTypeId}' not found.");

        var annual = await ResolveAnnualEntitlementAsync(employeeId, leaveTypeId, leaveSubTypeId, year, ct);
        var accrued = await GetAccruedAsOfAsync(employeeId, leaveTypeId, leaveSubTypeId, year, asOf, ct);

        var hasPolicy = await _leaveTypeRepository
            .GetQueryable()
            .Where(lt => lt.Id == leaveTypeId)
            .SelectMany(lt => lt.AccrualPolicies)
            .AnyAsync(p => p.IsActive && p.Frequency != AccrualFrequency.None, ct);

        DateOnly? accessibleFrom = null;
        var minMonths = leaveType.MinServiceMonthsToAccess ?? 0;
        if (minMonths > 0)
        {
            var employee = await _employeeRepository.GetByIdAsync(employeeId);
            if (employee?.DateEmployed is DateOnly hired)
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

    private async Task<Guid?> GetEmployeeStaffLevelIdAsync(Guid employeeId, CancellationToken ct)
    {
        return await _employeeRepository
            .GetQueryable()
            .Where(e => e.Id == employeeId)
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
