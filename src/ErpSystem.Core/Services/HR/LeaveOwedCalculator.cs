using ErpSystem.Core.Entities.HR.StaffLeave;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Core.Interfaces.HR.Services;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Core.Services.HR;

/// <inheritdoc cref="ILeaveOwedCalculator"/>
/// <remarks>
/// Extracted from <c>LeaveService.GetLeaveOwedAsync</c> (round 5, lane C6) unchanged, so the leaver's
/// settlement (lane L2) could ask the same question of one person. The report's own suite
/// (<c>run-round5-c.mjs</c>) is what proves the extraction moved nothing.
/// </remarks>
public class LeaveOwedCalculator : ILeaveOwedCalculator
{
    private readonly IGenericRepository<LeaveType> _leaveTypes;
    private readonly IGenericRepository<LeaveBalance> _balances;
    private readonly ILeaveUsageReader _usage;
    private readonly ILeaveEntitlementService _entitlement;
    private readonly ILeaveYearContext _leaveYear;
    private readonly ICurrentUserService _currentUser;

    public LeaveOwedCalculator(
        IGenericRepository<LeaveType> leaveTypes,
        IGenericRepository<LeaveBalance> balances,
        ILeaveUsageReader usage,
        ILeaveEntitlementService entitlement,
        ILeaveYearContext leaveYear,
        ICurrentUserService currentUser)
    {
        _leaveTypes = leaveTypes;
        _balances = balances;
        _usage = usage;
        _entitlement = entitlement;
        _leaveYear = leaveYear;
        _currentUser = currentUser;
    }

    public async Task<LeaveOwedComputation> ComputeAsync(
        DateOnly asOf, IReadOnlyList<LeaveAccrualSubject> people, bool accrueToYearEnd = false,
        CancellationToken ct = default)
    {
        if (_currentUser.TenantId is not Guid tenantId || tenantId == Guid.Empty)
            throw new InvalidOperationException("No tenant is associated with the current user.");

        var startMonth = await _leaveYear.StartMonthAsync(ct);
        var year = LeaveYear.For(asOf, startMonth);
        var yearStart = LeaveYear.StartOf(year, startMonth);
        var yearEnd = LeaveYear.EndOf(year, startMonth);

        // The tenant's annual leave: at most one type is Annual and active (round 5, A1).
        var annual = await _leaveTypes
            .GetQueryable()
            .FirstOrDefaultAsync(t => t.TenantId == tenantId && t.IsActive && t.Category == LeaveTypeCategory.Annual, ct)
            ?? throw new InvalidOperationException(
                "No leave type is set up as annual leave, so there is no annual leave to report. " +
                "Set the kind of the annual leave type to Annual.");

        var ids = people.Select(p => p.EmployeeId).Distinct().ToList();

        // The year's figures, one read each.
        var balances = (await _balances
                .GetQueryable()
                .Where(b => b.TenantId == tenantId && b.LeaveTypeId == annual.Id && b.Year == year && ids.Contains(b.EmployeeId))
                .Select(b => new
                {
                    b.EmployeeId, b.LeaveSubTypeId, b.CreatedAt,
                    b.EntitledDays, b.CarriedOverDays, b.AdjustmentDays, b.EncashedDays,
                })
                .ToListAsync(ct))
            // One row per employee and type is the rule (RecalculateAsync keys on it). Should a stray
            // second one exist, its counters are the same totals, so summing would double them.
            .GroupBy(b => b.EmployeeId)
            .ToDictionary(g => g.Key, g => g.OrderBy(b => b.LeaveSubTypeId != null).ThenBy(b => b.CreatedAt).First());

        // Taken, booked and pending leave by the date — from the shared reader (round 5, lane G), so
        // this, the year-end expiry run and reminder sweep 5 count exactly the same days.
        var usage = await _usage.ReadAsync(tenantId, annual.Id, yearStart, yearEnd, asOf, ids, ct);

        // A leaver's build-up is asked of the year's end, so the policy's ProRateOnExit — reading the
        // last day off the subject — is what stops the clock, or does not (see the interface).
        var snapshots = await _entitlement.GetSnapshotsAsync(
            people, annual.Id, year, accrueToYearEnd ? yearEnd : asOf, ct);

        // The day carried-in days lapse, as the forfeiture run's expiry step reads it — and, once it
        // has passed, the leave taken in time to use them.
        DateOnly? lapse = annual.CarryOverExpiryMonths is int expiryMonths ? yearStart.AddMonths(expiryMonths) : null;
        var usedBeforeLapse = lapse is DateOnly lapsed && asOf >= lapsed
            ? await _usage.ReadAsync(tenantId, annual.Id, yearStart, yearEnd, lapsed.AddDays(-1), ids, ct)
            : null;

        var figures = new Dictionary<Guid, LeaveOwedFigures>();
        foreach (var person in people)
        {
            var snapshot = snapshots[person.EmployeeId];
            balances.TryGetValue(person.EmployeeId, out var balance);

            var used = usage.TryGetValue(person.EmployeeId, out var u) ? u : new LeaveUsage(0m, 0m, 0m);
            var taken = used.TakenThrough;

            // Carried days count in full until the lapse; from it on, only those taken in time
            // (carried days are used first) — the rule the expiry run applies.
            var carried = balance?.CarriedOverDays ?? 0m;
            var carriedIn = usedBeforeLapse is null
                ? carried
                : Math.Min(carried, usedBeforeLapse.TryGetValue(person.EmployeeId, out var early) ? early.TakenThrough : 0m);

            // The same substitution the create check makes: a type that does not accrue hands over
            // the stored entitlement, an accruing one what has built up by the date.
            var entitled = balance?.EntitledDays ?? snapshot.AnnualEntitledDays;
            var adjustments = balance?.AdjustmentDays ?? 0m;
            var cashedIn = balance?.EncashedDays ?? 0m;

            figures[person.EmployeeId] = new LeaveOwedFigures(
                EntitledDays: entitled,
                BuiltUpTo: snapshot.HasAccrualPolicy ? snapshot.AccruedAsOf : null,
                BuiltUpDays: snapshot.HasAccrualPolicy ? snapshot.AccruedToDateDays : entitled,
                CarriedInDays: carriedIn,
                AdjustmentDays: adjustments,
                TakenDays: taken,
                CashedInDays: cashedIn,
                OwedDays: snapshot.AvailableFrom(entitled, carriedIn, adjustments, taken, 0m, cashedIn),
                BookedDays: used.TakenOrBooked - used.TakenThrough,
                AwaitingApprovalDays: used.Pending);
        }

        return new LeaveOwedComputation
        {
            AnnualType = annual,
            Year = year,
            YearStart = yearStart,
            YearEnd = yearEnd,
            CarryOverExpiresOn = lapse?.AddDays(-1),
            ByEmployee = figures,
        };
    }
}
