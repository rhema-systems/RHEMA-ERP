using ErpSystem.Core.Entities.HR.StaffLeave;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Common;
using ErpSystem.Core.Interfaces.HR;
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
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly ICurrentUserService _currentUserService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _clock;
    private readonly ILogger<LeaveYearEndService> _logger;

    public LeaveYearEndService(
        IGenericRepository<LeaveBalance> balanceRepository,
        IGenericRepository<LeaveType> leaveTypeRepository,
        IGenericRepository<LeaveAdjustment> adjustmentRepository,
        ILeaveBalanceRecalculationService recalculationService,
        ILeaveEntitlementService entitlementService,
        ICurrentUserProvider currentUserProvider,
        ICurrentUserService currentUserService,
        IUnitOfWork unitOfWork,
        IDateTimeProvider clock,
        ILogger<LeaveYearEndService> logger)
    {
        _balanceRepository = balanceRepository;
        _leaveTypeRepository = leaveTypeRepository;
        _adjustmentRepository = adjustmentRepository;
        _recalculationService = recalculationService;
        _entitlementService = entitlementService;
        _currentUserProvider = currentUserProvider;
        _currentUserService = currentUserService;
        _unitOfWork = unitOfWork;
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

    public async Task<LeaveYearEndResult> ProcessCarryOverAsync(
        int fromYear, Guid? employeeId = null, bool dryRun = false, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        var result = new LeaveYearEndResult { IsDryRun = dryRun };
        var toYear = fromYear + 1;

        var balances = await LoadBalancesAsync(fromYear, employeeId, ct);

        foreach (var balance in balances)
        {
            result.BalancesProcessed++;

            var leaveType = balance.LeaveType ?? await _leaveTypeRepository.GetByIdAsync(balance.LeaveTypeId);
            if (leaveType is null || leaveType.TenantId != tenantId || !leaveType.AllowCarryOver)
                continue;

            var remaining = balance.AvailableDays;
            if (remaining <= 0)
                continue;

            var carryAmount = leaveType.MaxCarryOverDays is int max && remaining > max ? max : remaining;

            // Find-or-create the next year's balance and set (not stack) its carried-over figure.
            var target = await _balanceRepository
                .GetQueryable()
                .FirstOrDefaultAsync(b => b.TenantId == tenantId
                                       && b.EmployeeId == balance.EmployeeId
                                       && b.LeaveTypeId == balance.LeaveTypeId
                                       && b.Year == toYear, ct);

            // ⚠ A dry run computes the same figure and writes nothing. It must still reach here,
            // past every guard above, or the preview would count balances the real run would skip.
            if (!dryRun)
            await _unitOfWork.ExecuteInTransactionAsync(async innerCt =>
            {
                if (target == null)
                {
                    var entitled = await _entitlementService.ResolveAnnualEntitlementAsync(
                        balance.EmployeeId, balance.LeaveTypeId, balance.LeaveSubTypeId, toYear, innerCt);
                    target = new LeaveBalance
                    {
                        TenantId        = tenantId,
                        EmployeeId      = balance.EmployeeId,
                        LeaveTypeId     = balance.LeaveTypeId,
                        LeaveSubTypeId  = balance.LeaveSubTypeId,
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
        result.Notes.Add(
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
        var result = new LeaveYearEndResult();
        var effectiveAsOf = asOf ?? _clock.TodayUtc;
        var yearStart = new DateOnly(year, 1, 1);

        var balances = await LoadBalancesAsync(year, employeeId, ct);

        foreach (var balance in balances)
        {
            result.BalancesProcessed++;

            var leaveType = balance.LeaveType ?? await _leaveTypeRepository.GetByIdAsync(balance.LeaveTypeId);
            if (leaveType is null || leaveType.TenantId != tenantId)
                continue;

            bool affected = false;

            // 1) Expire carried-over days whose carry-over window has elapsed.
            if (leaveType.CarryOverExpiryMonths is int expiryMonths
                && balance.CarriedOverDays > 0
                && effectiveAsOf >= yearStart.AddMonths(expiryMonths))
            {
                // ⚠ Dry run: compute, count, write nothing.
                if (!dryRun)
                await _unitOfWork.ExecuteInTransactionAsync(async innerCt =>
                {
                    balance.CarriedOverDays = 0m;
                    await _balanceRepository.UpdateAsync(balance);
                    await _unitOfWork.SaveChangesAsync(innerCt);
                }, ct);
                affected = true;
                result.Notes.Add($"Expired carried-over days for employee {balance.EmployeeId}, leave type {leaveType.Name}.");
            }

            // 2) Forfeit unused accrual after the forfeiture cut-off.
            if (leaveType.ForfeitUnusedAfterMonths is int forfeitMonths
                && effectiveAsOf >= yearStart.AddMonths(forfeitMonths))
            {
                var alreadyForfeited = await _adjustmentRepository
                    .GetQueryable()
                    .AnyAsync(a => a.TenantId == tenantId && a.LeaveBalanceId == balance.Id && a.Reason == ForfeitureReason, ct);

                var unused = balance.AvailableDays;
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
                    result.Notes.Add($"Forfeited {unused} unused day(s) for employee {balance.EmployeeId}, leave type {leaveType.Name}.");
                }
            }

            if (affected) result.BalancesAffected++;
        }

        result.BalancesSkipped = result.BalancesProcessed - result.BalancesAffected;
        result.Notes.Add(
            $"Examined {result.BalancesProcessed} balance(s): forfeited {result.TotalDaysForfeited} day(s) "
            + $"across {result.BalancesAffected}, left {result.BalancesSkipped} alone."
            + (dryRun ? " NOTHING WAS WRITTEN - this was a dry run." : string.Empty));

        _logger.LogInformation(
            "Leave forfeiture {Mode} for {Year} as of {AsOf}: {Affected} of {Examined} balances, {Days} days forfeited",
            dryRun ? "previewed" : "processed", year, effectiveAsOf,
            result.BalancesAffected, result.BalancesProcessed, result.TotalDaysForfeited);
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
