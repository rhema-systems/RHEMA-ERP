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
/// Admin-triggered year-end / cut-off processing — see <see cref="ILeaveYearEndService"/>.
/// </summary>
public class LeaveYearEndService : ILeaveYearEndService
{
    /// <summary>Marker on the auto-posted forfeiture adjustment, used to keep the run idempotent.</summary>
    private const string ForfeitureReason = "FORFEIT: unused leave (year-end/cut-off)";

    private readonly IGenericRepository<LeaveBalance> _balanceRepository;
    private readonly IGenericRepository<LeaveType> _leaveTypeRepository;
    private readonly IGenericRepository<LeaveAdjustment> _adjustmentRepository;
    private readonly ILeaveBalanceRecalculationService _recalculationService;
    private readonly ILeaveEntitlementService _entitlementService;
    private readonly ILeaveUsageReader _usage;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly ICurrentUserService _currentUserService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _clock;
    private readonly ILeaveYearContext _leaveYear;
    private readonly ILogger<LeaveYearEndService> _logger;

    public LeaveYearEndService(
        IGenericRepository<LeaveBalance> balanceRepository,
        IGenericRepository<LeaveType> leaveTypeRepository,
        IGenericRepository<LeaveAdjustment> adjustmentRepository,
        ILeaveBalanceRecalculationService recalculationService,
        ILeaveEntitlementService entitlementService,
        ILeaveUsageReader usage,
        ICurrentUserProvider currentUserProvider,
        ICurrentUserService currentUserService,
        IUnitOfWork unitOfWork,
        IDateTimeProvider clock,
        ILeaveYearContext leaveYear,
        ILogger<LeaveYearEndService> logger)
    {
        _balanceRepository = balanceRepository;
        _leaveTypeRepository = leaveTypeRepository;
        _adjustmentRepository = adjustmentRepository;
        _recalculationService = recalculationService;
        _entitlementService = entitlementService;
        _usage = usage;
        _currentUserProvider = currentUserProvider;
        _currentUserService = currentUserService;
        _unitOfWork = unitOfWork;
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

    /// <summary>
    /// The days a year-end run treats as unused, per the leave type's <c>YearEndBasis</c>
    /// (entitlement plan B2, decision D-2).
    /// </summary>
    /// <remarks>
    /// <para><b>Granted</b> — the default, and what both runs did before this existed — reads
    /// the balance built on the whole year's <c>EntitledDays</c>. A mid-year joiner who accrued 3.5
    /// days and took none therefore carries the full cap.</para>
    ///
    /// <para><b>Earned</b> substitutes accrued-to-date, through the same definition the create check
    /// and every balance read use — so "what you may carry" and "what you could have booked" cannot
    /// disagree.</para>
    ///
    /// <para>⚠ <b>Accrual is asked as at the END of the year being closed</b>, not as at today. A
    /// carry-over run in February for the year just gone must not credit somebody with two months of
    /// the year they are now in, and a run for the CURRENT year must not count months that have not
    /// happened. Passing the year end and letting the engine clamp does both.</para>
    ///
    /// <para>⚠ <paramref name="carried"/> is the balance's carried days <b>after the lapse</b> (round
    /// 5, lane G) — the ones still usable, not what the row holds. Carried days not taken in time
    /// are gone whether or not anybody has run expiry, so neither carry-over nor forfeiture may count
    /// them as unused.</para>
    /// </remarks>
    private async Task<decimal> UnusedDaysAsync(
        LeaveBalance balance, LeaveType leaveType, int year, decimal carried, CancellationToken ct)
    {
        if (leaveType.YearEndBasis != LeaveYearEndBasis.Earned)
            return balance.EntitledDays + carried + balance.AdjustmentDays
                 - balance.UsedDays - balance.PendingDays - balance.EncashedDays;

        // ⚠ The EARLIER of the year end and today. The engine clamps a later date down to the year
        // end, so a completed year asks as at 31 December — but it does NOT clamp to today, and
        // passing the year end for the CURRENT year would credit months that have not happened yet.
        // Under Earned that would carry days nobody has earned, which is the one thing the setting
        // exists to prevent.
        var yearEnd = LeaveYear.EndOf(year, await _leaveYear.StartMonthAsync());
        var today = _clock.TodayUtc;
        var asOf = today < yearEnd ? today : yearEnd;

        var snapshot = await _entitlementService.GetSnapshotAsync(
            balance.EmployeeId, balance.LeaveTypeId, balance.LeaveSubTypeId, year, asOf, ct);

        return snapshot.AvailableFrom(
            balance.EntitledDays, carried, balance.AdjustmentDays,
            balance.UsedDays, balance.PendingDays, balance.EncashedDays);
    }

    /// <summary>
    /// The carried days of <paramref name="balance"/> still usable as at <paramref name="asOf"/>:
    /// all of them until the leave type's carry-over expiry, and from it on only those taken in time
    /// (round 5, lane G).
    /// </summary>
    /// <remarks>
    /// <para><b>Carried days are used first.</b> So from the lapse, the days that survive are the
    /// carried days covered by annual leave taken on or before the last usable day, and the rest
    /// lapse. Leave booked for after the lapse does not save them: carried days must be taken in time,
    /// not merely asked for. It used to zero the carried figure outright, which charged the days
    /// somebody HAD taken from it a second time (guide § 23, L-42).</para>
    ///
    /// <para>The days used in time come from <see cref="ILeaveUsageReader"/>, which reminder sweep 5
    /// and the leave owed report also read — so the run removes exactly what the reminder warned
    /// about. Read once per leave type per run, for every employee in scope.</para>
    /// </remarks>
    private async Task<decimal> CarriedStillUsableAsync(
        LeaveBalance balance, LeaveType leaveType, int year, DateOnly asOf,
        Dictionary<Guid, IReadOnlyDictionary<Guid, LeaveUsage>> usedInTime, Guid? employeeId, CancellationToken ct)
    {
        if (balance.CarriedOverDays <= 0 || leaveType.CarryOverExpiryMonths is not int expiryMonths)
            return balance.CarriedOverDays;

        var startMonth = await _leaveYear.StartMonthAsync(ct);
        var yearStart = LeaveYear.StartOf(year, startMonth);
        var lapse = yearStart.AddMonths(expiryMonths);
        if (asOf < lapse)
            return balance.CarriedOverDays;

        if (!usedInTime.TryGetValue(leaveType.Id, out var byEmployee))
        {
            byEmployee = await _usage.ReadAsync(
                GetTenantId(), leaveType.Id, yearStart, LeaveYear.EndOf(year, startMonth), lapse.AddDays(-1),
                employeeId.HasValue ? new[] { employeeId.Value } : null, ct);
            usedInTime[leaveType.Id] = byEmployee;
        }

        var used = byEmployee.TryGetValue(balance.EmployeeId, out var u) ? u.TakenThrough : 0m;
        return Math.Min(balance.CarriedOverDays, used);
    }

    public async Task<LeaveYearEndResult> ProcessCarryOverAsync(
        int fromYear, Guid? employeeId = null, bool dryRun = false, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        var fromYearEnd = LeaveYear.EndOf(fromYear, await _leaveYear.StartMonthAsync(ct));

        // ⚠ Round 5, lane G: a year that has not ended cannot be closed. Carrying from it would move
        // days that can still be taken or asked for — and every day approved afterwards would be
        // counted once where it is taken and again in the days already carried. A preview can run at
        // any time, which is how a run is checked before the year turns.
        if (!dryRun && _clock.TodayUtc <= fromYearEnd)
            throw new InvalidOperationException(
                $"The {fromYear} leave year ends on {fromYearEnd:d MMMM yyyy}, so its unused days cannot be carried over yet. "
                + $"Run it from {fromYearEnd.AddDays(1):d MMMM yyyy}; a preview can run at any time.");

        var result = new LeaveYearEndResult { IsDryRun = dryRun };
        var toYear = fromYear + 1;
        var usedInTime = new Dictionary<Guid, IReadOnlyDictionary<Guid, LeaveUsage>>();

        var balances = await LoadBalancesAsync(fromYear, employeeId, ct);

        // ⚠ Round 5, lane G: one pot per leave type (the rule lane N settled — a balance is kept per
        // type, and every sub-type draws on it). A stray second row for the same employee, type and
        // year would otherwise be carried too, and "set, not stacked" would let whichever came last
        // overwrite the other. The type's own row is the pot; any other is examined and named.
        foreach (var pot in balances.GroupBy(b => (b.EmployeeId, b.LeaveTypeId)))
        {
            var rows = pot.OrderBy(b => b.LeaveSubTypeId != null).ThenBy(b => b.CreatedAt).ToList();
            var balance = rows[0];
            result.BalancesProcessed += rows.Count;
            if (rows.Count > 1)
                result.Notes.Add(
                    $"⚠ {rows.Count} balances for one leave type in {fromYear} (employee {balance.EmployeeId}): "
                    + "carried from the type's own row only.");

            var leaveType = balance.LeaveType ?? await _leaveTypeRepository.GetByIdAsync(balance.LeaveTypeId);
            if (leaveType is null || leaveType.TenantId != tenantId || !leaveType.AllowCarryOver)
                continue;

            // The closing year's own carried days, as they stood at its end: lapsed ones do not travel
            // again, whether or not anybody ran expiry on them.
            var carried = await CarriedStillUsableAsync(
                balance, leaveType, fromYear, fromYearEnd, usedInTime, employeeId, ct);
            var remaining = await UnusedDaysAsync(balance, leaveType, fromYear, carried, ct);
            if (remaining <= 0)
                continue;

            var carryAmount = leaveType.MaxCarryOverDays is int max && remaining > max ? max : remaining;

            // Find-or-create the next year's balance and set (not stack) its carried-over figure. The
            // type's own row, as above; a new one is made for the type, never for a sub-type.
            var target = await _balanceRepository
                .GetQueryable()
                .Where(b => b.TenantId == tenantId
                         && b.EmployeeId == balance.EmployeeId
                         && b.LeaveTypeId == balance.LeaveTypeId
                         && b.Year == toYear)
                .OrderBy(b => b.LeaveSubTypeId != null)
                .ThenBy(b => b.CreatedAt)
                .FirstOrDefaultAsync(ct);

            // ⚠ A dry run computes the same figure and writes nothing. It must still reach here,
            // past every guard above, or the preview would count balances the real run would skip.
            if (!dryRun)
            await _unitOfWork.ExecuteInTransactionAsync(async innerCt =>
            {
                if (target == null)
                {
                    var entitled = await _entitlementService.ResolveAnnualEntitlementAsync(
                        balance.EmployeeId, balance.LeaveTypeId, null, toYear, innerCt);
                    target = new LeaveBalance
                    {
                        TenantId        = tenantId,
                        EmployeeId      = balance.EmployeeId,
                        LeaveTypeId     = balance.LeaveTypeId,
                        LeaveSubTypeId  = null,
                        Year            = toYear,
                        EntitledDays    = entitled,
                        CarriedOverDays = carryAmount
                    };
                    await _balanceRepository.AddAsync(target);
                }
                else
                {
                    target.CarriedOverDays = carryAmount;
                    await _balanceRepository.UpdateAsync(target);
                }
                await _unitOfWork.SaveChangesAsync(innerCt);
            }, ct);

            result.BalancesAffected++;
            result.TotalDaysCarriedOver += carryAmount;
        }

        result.BalancesSkipped = result.BalancesProcessed - result.BalancesAffected;
        result.Notes.Insert(0,
            $"Examined {result.BalancesProcessed} balance(s): carried over {result.TotalDaysCarriedOver} day(s) "
            + $"from {fromYear} into {toYear} across {result.BalancesAffected}, left {result.BalancesSkipped} alone."
            + (dryRun ? " NOTHING WAS WRITTEN - this was a dry run." : string.Empty));
        _logger.LogInformation(
            "Leave carry-over {Mode} for {FromYear}->{ToYear}: {Affected} of {Examined} balances, {Days} days",
            dryRun ? "previewed" : "processed", fromYear, toYear,
            result.BalancesAffected, result.BalancesProcessed, result.TotalDaysCarriedOver);
        return result;
    }

    public async Task<LeaveYearEndResult> ProcessForfeitureAsync(
        int year, DateOnly? asOf = null, Guid? employeeId = null, bool dryRun = false, CancellationToken ct = default)
    {
        var performedBy = _currentUserService.EmployeeId is Guid actor && actor != Guid.Empty
            ? actor
            : throw new InvalidOperationException(
                "Posting a leave forfeiture requires your user account to be linked to an employee record. Please contact your administrator.");
        var tenantId = GetTenantId();

        // ⚠ Round 5, lane G: the preview said nothing about being one. The carry-over result has
        // always carried the flag; this one never set it, so the screen's "preview only" banner never
        // showed on a forfeiture preview.
        var result = new LeaveYearEndResult { IsDryRun = dryRun };
        var effectiveAsOf = asOf ?? _clock.TodayUtc;
        var startMonth = await _leaveYear.StartMonthAsync();
        var yearStart = LeaveYear.StartOf(year, startMonth);
        var usedInTime = new Dictionary<Guid, IReadOnlyDictionary<Guid, LeaveUsage>>();

        // ⚠ Leave settings audit 2, L-90: a year's UNUSED days are not forfeited while the year is open
        // — they can still be booked, and the field's own help says forfeiture removes what a year that
        // has ended can no longer book. Carry-over refuses an open year outright; here only the
        // forfeiture waits, because the run's first step — expiring carried days past their window —
        // belongs mid-year and still runs. The preview skips it too, so it shows what the run would do.
        var yearEnd = LeaveYear.EndOf(year, startMonth);
        var yearOpen = _clock.TodayUtc <= yearEnd;
        var forfeitureHeld = false;

        var balances = await LoadBalancesAsync(year, employeeId, ct);

        foreach (var balance in balances)
        {
            result.BalancesProcessed++;

            var leaveType = balance.LeaveType ?? await _leaveTypeRepository.GetByIdAsync(balance.LeaveTypeId);
            if (leaveType is null || leaveType.TenantId != tenantId)
                continue;

            bool affected = false;

            // 1) Expire the carried-over days not taken before their window closed — and only those
            //    (round 5, lane G). The days taken in time stay: they have been used, and removing
            //    them charged the employee for them twice.
            var carriedBefore = balance.CarriedOverDays;
            var carried = await CarriedStillUsableAsync(
                balance, leaveType, year, effectiveAsOf, usedInTime, employeeId, ct);
            if (carried < carriedBefore)
            {
                var lapsed = carriedBefore - carried;

                // ⚠ Dry run: compute, count, write nothing.
                if (!dryRun)
                await _unitOfWork.ExecuteInTransactionAsync(async innerCt =>
                {
                    balance.CarriedOverDays = carried;
                    await _balanceRepository.UpdateAsync(balance);
                    await _unitOfWork.SaveChangesAsync(innerCt);
                }, ct);
                affected = true;
                result.TotalDaysExpired += lapsed;
                result.Notes.Add(
                    $"Expired {lapsed:0.##} of {carriedBefore:0.##} carried-over day(s) for employee {balance.EmployeeId}, "
                    + $"leave type {leaveType.Name}: {carried:0.##} were taken before "
                    + $"{yearStart.AddMonths(leaveType.CarryOverExpiryMonths!.Value):d MMM yyyy}.");
            }

            // 2) Forfeit unused accrual after the forfeiture cut-off — once the year has ended (L-90).
            if (leaveType.ForfeitUnusedAfterMonths is int forfeitMonths
                && effectiveAsOf >= yearStart.AddMonths(forfeitMonths)
                && yearOpen)
            {
                forfeitureHeld = true;
            }
            else if (leaveType.ForfeitUnusedAfterMonths is int forfeitMonths2
                && effectiveAsOf >= yearStart.AddMonths(forfeitMonths2))
            {
                var alreadyForfeited = await _adjustmentRepository
                    .GetQueryable()
                    .AnyAsync(a => a.TenantId == tenantId && a.LeaveBalanceId == balance.Id && a.Reason == ForfeitureReason, ct);

                // ⚠ Forfeiture follows the SAME basis, which is why the setting is not called
                // "carry-over basis". Under Earned it removes what the employee earned and did not
                // take; the part of the grant they never accrued is simply not forfeited, because
                // it was never theirs to lose. That reads oddly against "use it or lose it" until
                // you notice the alternative is forfeiting days the employee could not have taken.
                //
                // ⚠ Counted after the expiry above, with the carried days still usable — in a preview
                // too, where the balance itself was not touched — so a lapsed day is not forfeited as
                // well, and the preview forfeits exactly what the run would.
                var unused = await UnusedDaysAsync(balance, leaveType, year, carried, ct);
                if (!alreadyForfeited && unused > 0)
                {
                    // ⚠ Dry run: compute, count, write nothing.
                    if (!dryRun)
                    await _unitOfWork.ExecuteInTransactionAsync(async innerCt =>
                    {
                        var adjustment = new LeaveAdjustment
                        {
                            TenantId       = tenantId,
                            LeaveBalanceId = balance.Id,
                            EmployeeId     = balance.EmployeeId,
                            LeaveTypeId    = balance.LeaveTypeId,
                            LeaveSubTypeId = balance.LeaveSubTypeId,
                            Year           = balance.Year,
                            Days           = -unused,
                            Reason         = ForfeitureReason,
                            AdjustmentDate = _clock.UtcNow,
                            // ⚠ Finish-plan lane 4 (2026-09-01): this was Guid.Empty ("system-posted").
                            // PerformedBy is a REQUIRED Employee foreign key and no employee has the
                            // empty id, so every forfeiture ever posted would have failed on the
                            // constraint — the endpoint's only harness evidence was a 403 check, so
                            // the insert had never run. The forfeiture is raised by the HR admin who
                            // calls the endpoint; they are its actor.
                            PerformedBy    = performedBy
                        };
                        await _adjustmentRepository.AddAsync(adjustment);
                        await _unitOfWork.SaveChangesAsync(innerCt);
                        await _recalculationService.RecalculateAsync(balance.EmployeeId, balance.LeaveTypeId, balance.Year);
                    }, ct);

                    affected = true;
                    result.TotalDaysForfeited += unused;
                    result.Notes.Add($"Forfeited {unused:0.##} unused day(s) for employee {balance.EmployeeId}, leave type {leaveType.Name}.");
                }
            }

            if (affected) result.BalancesAffected++;
        }

        result.BalancesSkipped = result.BalancesProcessed - result.BalancesAffected;
        result.Notes.Insert(0,
            $"Examined {result.BalancesProcessed} balance(s): expired {result.TotalDaysExpired:0.##} carried day(s) "
            + $"and forfeited {result.TotalDaysForfeited:0.##} day(s) across {result.BalancesAffected}, "
            + $"left {result.BalancesSkipped} alone."
            + (dryRun ? " NOTHING WAS WRITTEN - this was a dry run." : string.Empty));
        if (forfeitureHeld)
            result.Notes.Insert(1,
                $"The {year} leave year runs to {yearEnd:d MMMM yyyy}, so its unused days were not forfeited: until then "
                + "they can still be booked. Carried days past their window were expired as usual. Run it again from "
                + $"{yearEnd.AddDays(1):d MMMM yyyy} to forfeit what is left.");

        _logger.LogInformation(
            "Leave forfeiture {Mode} for {Year} as of {AsOf}: {Affected} of {Examined} balances, {Expired} days expired, {Days} days forfeited",
            dryRun ? "previewed" : "processed", year, effectiveAsOf,
            result.BalancesAffected, result.BalancesProcessed, result.TotalDaysExpired, result.TotalDaysForfeited);
        return result;
    }

    private async Task<List<LeaveBalance>> LoadBalancesAsync(int year, Guid? employeeId, CancellationToken ct)
    {
        var tenantId = GetTenantId();
        var query = _balanceRepository
            .GetQueryable()
            .Include(b => b.LeaveType)
            .Where(b => b.TenantId == tenantId && b.Year == year);

        if (employeeId.HasValue)
            query = query.Where(b => b.EmployeeId == employeeId.Value);

        return await query.ToListAsync(ct);
    }
}
