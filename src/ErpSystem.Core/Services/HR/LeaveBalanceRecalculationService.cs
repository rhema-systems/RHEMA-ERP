using ErpSystem.Core.Entities.HR.StaffLeave;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Core.Interfaces.HR.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR;

/// <summary>
/// Recomputes leave balance counters from source data (requests + adjustments).
/// Never touches EntitledDays or CarriedOverDays.
/// </summary>
public class LeaveBalanceRecalculationService : ILeaveBalanceRecalculationService
{
    private readonly IGenericRepository<LeaveBalance> _leaveBalanceRepository;
    private readonly IGenericRepository<LeaveRequest> _leaveRequestRepository;
    private readonly IGenericRepository<LeaveAdjustment> _leaveAdjustmentRepository;
    private readonly IGenericRepository<LeaveEncashment> _leaveEncashmentRepository;
    private readonly IGenericRepository<LeaveType> _leaveTypeRepository;
    private readonly ILeaveEntitlementService _entitlementService;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILeaveYearContext _leaveYear;
    private readonly ILogger<LeaveBalanceRecalculationService> _logger;

    public LeaveBalanceRecalculationService(
        IGenericRepository<LeaveBalance> leaveBalanceRepository,
        IGenericRepository<LeaveRequest> leaveRequestRepository,
        IGenericRepository<LeaveAdjustment> leaveAdjustmentRepository,
        IGenericRepository<LeaveEncashment> leaveEncashmentRepository,
        IGenericRepository<LeaveType> leaveTypeRepository,
        ILeaveEntitlementService entitlementService,
        ICurrentUserProvider currentUserProvider,
        IUnitOfWork unitOfWork,
        ILeaveYearContext leaveYear,
        ILogger<LeaveBalanceRecalculationService> logger)
    {
        _leaveBalanceRepository = leaveBalanceRepository;
        _leaveRequestRepository = leaveRequestRepository;
        _leaveAdjustmentRepository = leaveAdjustmentRepository;
        _leaveEncashmentRepository = leaveEncashmentRepository;
        _leaveTypeRepository = leaveTypeRepository;
        _entitlementService = entitlementService;
        _currentUserProvider = currentUserProvider;
        _unitOfWork = unitOfWork;
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

    /// <inheritdoc />
    public async Task RecalculateAsync(Guid employeeId, Guid leaveTypeId, int year)
    {
        var tenantId = GetTenantId();

        var leaveType = await _leaveTypeRepository.GetByIdAsync(leaveTypeId);
        if (leaveType == null || leaveType.TenantId != tenantId)
            throw new ArgumentException($"Leave type '{leaveTypeId}' not found.");

        // ⚠ Entitlement plan C1. A request belongs to the leave year its START DATE falls in, and
        // these bounds are how that is asked of the database. `r.StartDate.Year == year` could not
        // survive a leave year that does not start in January — and as a bonus this form is
        // sargable, where the old one wrapped the column in YEAR() and could not use an index.
        //
        // ⚠ The ADJUSTMENT query below deliberately keeps `a.Year == year`: that is a stored int
        // LABEL on the row, not a date, and it is unaffected by where the year begins.
        var yearStart = LeaveYear.StartOf(year, await _leaveYear.StartMonthAsync());
        var yearEnd = LeaveYear.EndOf(year, await _leaveYear.StartMonthAsync());

        var balance = await _leaveBalanceRepository
            .GetQueryable()
            .FirstOrDefaultAsync(lb =>
                lb.TenantId == tenantId &&
                lb.EmployeeId == employeeId &&
                lb.LeaveTypeId == leaveTypeId &&
                lb.Year == year);

        var isNewBalance = balance == null;
        if (balance == null)
        {
            // Resolve the full annual entitlement via the entitlement engine (subtype cap →
            // effective-dated allocation → leave-type default) rather than blindly using the
            // default-days figure.
            var entitled = await _entitlementService.ResolveAnnualEntitlementAsync(employeeId, leaveTypeId, null, year);
            balance = new LeaveBalance
            {
                TenantId        = tenantId,
                EmployeeId      = employeeId,
                LeaveTypeId     = leaveTypeId,
                Year            = year,
                EntitledDays    = entitled,
                CarriedOverDays = 0,
                UsedDays        = 0,
                PendingDays     = 0,
                AdjustmentDays  = 0
            };
            await _leaveBalanceRepository.AddAsync(balance);
        }

        // UsedDays — sum of every request whose days are actually being taken.
        //
        // ⚠ This used to be `Status == Approved` alone, and that was a defect with a long fuse.
        // CloseLeaveRequestAsync sets the status to Completed, and closing does not itself
        // recalculate — so the days were not returned at closure, they were returned by the NEXT
        // recalculation, whenever that happened to run (the next approval, cancellation or
        // adjustment for the same employee and type). An employee who took five days and had the
        // leave closed silently got those five days back, attributed to nothing and traceable to
        // nothing. The register and the compliance read, which both ask what has been TAKEN, were
        // wrong the same way.
        //
        // ⚠ InProgress belongs here too and is no longer hypothetical: leave that is happening
        // right now has been taken, not requested. Before R-14 nothing ever assigned that status,
        // so the question never arose; the sweep that advances a request into it now makes this
        // load-bearing. Had it been left below, a day's leave would have jumped from Used to
        // Pending the morning it started.
        //
        // The three statuses here are exactly LeaveService.CountsAsTaken, and they must stay that
        // way: that method decides whether attendance days exist for a request, so a status counted
        // as taken on the attendance register and not in the balance would put the two permanently
        // out of step.
        balance.UsedDays = await _leaveRequestRepository
            .GetQueryable()
            .Where(r =>
                r.TenantId == tenantId &&
                r.EmployeeId  == employeeId &&
                r.LeaveTypeId == leaveTypeId &&
                r.StartDate >= yearStart && r.StartDate <= yearEnd &&
                (r.Status == LeaveStatus.Approved ||
                 r.Status == LeaveStatus.InProgress ||
                 r.Status == LeaveStatus.Completed))
            .SumAsync(r => (decimal?)r.TotalDays) ?? 0m;

        // PendingDays — days asked for and not yet granted.
        //
        // ⚠ InProgress moved OUT of here and into UsedDays above. It must not appear in both:
        // AvailableDays subtracts Used and Pending separately, so a status counted twice would
        // charge the employee twice for the same leave.
        balance.PendingDays = await _leaveRequestRepository
            .GetQueryable()
            .Where(r =>
                r.TenantId == tenantId &&
                r.EmployeeId  == employeeId &&
                r.LeaveTypeId == leaveTypeId &&
                r.StartDate >= yearStart && r.StartDate <= yearEnd &&
                r.Status == LeaveStatus.Pending)
            .SumAsync(r => (decimal?)r.TotalDays) ?? 0m;

        // AdjustmentDays — sum of all adjustments linked to this balance
        balance.AdjustmentDays = await _leaveAdjustmentRepository
            .GetQueryable()
            .Where(a =>
                a.TenantId == tenantId &&
                a.EmployeeId  == employeeId &&
                a.LeaveTypeId == leaveTypeId &&
                a.Year        == year)
            .SumAsync(a => (decimal?)a.Days) ?? 0m;

        // EncashedDays — sum of approved and processed encashments (single source of truth; the
        // encashment service no longer mutates UsedDays directly, so a recalc can't erase the deduction).
        //
        // ⚠ Round 5, lane L3: from APPROVAL, not from payment. Counting only Processed left an
        // approved encashment's days free to be taken as leave in the weeks before Finance paid it —
        // the same days sold and spent. Defensive: it matters only where in-service encashment is on.
        balance.EncashedDays = await _leaveEncashmentRepository
            .GetQueryable()
            .Where(e =>
                e.TenantId == tenantId &&
                e.EmployeeId  == employeeId &&
                e.LeaveTypeId == leaveTypeId &&
                e.Year        == year &&
                (e.Status == LeaveEncashmentStatus.Approved || e.Status == LeaveEncashmentStatus.Processed))
            .SumAsync(e => (decimal?)e.DaysEncashed) ?? 0m;

        // A newly-created balance is already tracked as Added; calling UpdateAsync would flip it
        // to Modified and EF would emit an UPDATE for a non-existent row (0 rows → concurrency
        // exception). Only mark existing balances as updated.
        if (!isNewBalance)
            await _leaveBalanceRepository.UpdateAsync(balance);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogDebug(
            "Leave balance recalculated: employee={EmployeeId} type={LeaveTypeId} year={Year} " +
            "used={Used} pending={Pending} adjustment={Adjustment} encashed={Encashed}",
            employeeId, leaveTypeId, year,
            balance.UsedDays, balance.PendingDays, balance.AdjustmentDays, balance.EncashedDays);
    }

    /// <inheritdoc />
    public async Task RecalculateAllAsync(Guid employeeId, int year)
    {
        var tenantId = GetTenantId();

        var leaveTypeIds = await _leaveBalanceRepository
            .GetQueryable()
            .Where(lb => lb.TenantId == tenantId && lb.EmployeeId == employeeId && lb.Year == year)
            .Select(lb => lb.LeaveTypeId)
            .Distinct()
            .ToListAsync();

        foreach (var leaveTypeId in leaveTypeIds)
        {
            await RecalculateAsync(employeeId, leaveTypeId, year);
        }
    }

    /// <inheritdoc />
    public async Task<LeaveBulkRecalculationResult> RecalculateTenantAsync(
        int year, Guid? leaveTypeId = null, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        var result = new LeaveBulkRecalculationResult { Year = year, LeaveTypeId = leaveTypeId };

        // Driven off the BALANCES that exist, not off the employee register. A recalculation
        // re-derives figures onto rows that are already there; walking every employee would mint
        // balances for people who have never had one, which is a different operation entirely and
        // not what a correction pass is for.
        var pairs = await _leaveBalanceRepository
            .GetQueryable()
            .Where(lb => lb.TenantId == tenantId
                      && lb.Year == year
                      && (leaveTypeId == null || lb.LeaveTypeId == leaveTypeId))
            .Select(lb => new { lb.EmployeeId, lb.LeaveTypeId })
            .Distinct()
            .ToListAsync(ct);

        foreach (var pair in pairs.GroupBy(p => p.EmployeeId))
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                foreach (var type in pair.Select(p => p.LeaveTypeId).Distinct())
                    await RecalculateAsync(pair.Key, type, year);

                result.EmployeesProcessed++;
            }
            catch (Exception ex)
            {
                // ⚠ One employee's failure must not abandon the other 899. A correction pass that
                // stops halfway leaves the tenant in a worse state than one that never ran, because
                // nobody can tell which half is current.
                result.EmployeesFailed++;
                result.Notes.Add($"Employee {pair.Key}: {ex.Message}");
                _logger.LogError(ex, "Bulk leave recalculation failed for employee {EmployeeId}", pair.Key);
            }
        }

        result.Notes.Insert(0,
            $"Recalculated {result.EmployeesProcessed} employee(s) for {year}"
            + (leaveTypeId is null ? " across every leave type" : " on one leave type")
            + (result.EmployeesFailed > 0 ? $"; {result.EmployeesFailed} failed." : "."));

        _logger.LogInformation(
            "Bulk leave recalculation for {Year}: {Processed} employee(s), {Failed} failed",
            year, result.EmployeesProcessed, result.EmployeesFailed);

        return result;
    }

    /// <inheritdoc />
    public async Task<LeaveEntitlementRepairResult> RepairEntitlementsAsync(
        int year, Guid? leaveTypeId = null, Guid? employeeId = null, bool dryRun = false,
        CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        var result = new LeaveEntitlementRepairResult { Year = year, IsDryRun = dryRun };

        // Driven off the balances that exist, like the recalculation above and for the same reason:
        // this pass CORRECTS stored rows. Minting a balance for somebody who never had one is a
        // different operation and not what a repair is for.
        var balances = await _leaveBalanceRepository
            .GetQueryable()
            .Include(lb => lb.Employee)
            .Include(lb => lb.LeaveType)
            .Where(lb => lb.TenantId == tenantId
                      && lb.Year == year
                      && (leaveTypeId == null || lb.LeaveTypeId == leaveTypeId)
                      && (employeeId == null || lb.EmployeeId == employeeId))
            .OrderBy(lb => lb.EmployeeId)
            .ToListAsync(ct);

        // One query for the whole pass rather than one per changed row: which of these balances
        // already carried days into next year, and therefore has a carry-over computed from the
        // figure this pass is about to replace.
        var carriedForward = await _leaveBalanceRepository
            .GetQueryable()
            .Where(lb => lb.TenantId == tenantId && lb.Year == year + 1 && lb.CarriedOverDays > 0)
            .Select(lb => new { lb.EmployeeId, lb.LeaveTypeId })
            .ToListAsync(ct);
        var carriedKeys = carriedForward
            .Select(c => (c.EmployeeId, c.LeaveTypeId))
            .ToHashSet();

        foreach (var balance in balances)
        {
            ct.ThrowIfCancellationRequested();
            result.BalancesExamined++;

            decimal resolved;
            try
            {
                resolved = await _entitlementService.ResolveAnnualEntitlementAsync(
                    balance.EmployeeId, balance.LeaveTypeId, balance.LeaveSubTypeId, year, ct);
            }
            catch (Exception ex)
            {
                // ⚠ One unresolvable row must not abandon the rest — the same rule the bulk
                // recalculation follows. A half-finished correction is worse than none, because
                // nobody can tell which half is current.
                result.BalancesFailed++;
                result.Notes.Add($"{Describe(balance)}: could not resolve entitlement — {ex.Message}");
                _logger.LogError(ex,
                    "Entitlement repair could not resolve employee {EmployeeId} type {LeaveTypeId} for {Year}",
                    balance.EmployeeId, balance.LeaveTypeId, year);
                continue;
            }

            if (resolved == balance.EntitledDays)
            {
                result.BalancesAlreadyCorrect++;
                continue;
            }

            var carryRun = carriedKeys.Contains((balance.EmployeeId, balance.LeaveTypeId));
            if (carryRun) result.ChangedWithCarryOverAlreadyRun++;

            result.Notes.Add(
                $"{Describe(balance)}: {balance.EntitledDays:0.##} → {resolved:0.##}"
                + (carryRun ? " ⚠ carry-over into the next year was already computed from the old figure." : string.Empty));

            if (!dryRun)
            {
                await _unitOfWork.ExecuteInTransactionAsync(async innerCt =>
                {
                    balance.EntitledDays = resolved;
                    await _leaveBalanceRepository.UpdateAsync(balance);
                    await _unitOfWork.SaveChangesAsync(innerCt);
                }, ct);
            }

            result.BalancesChanged++;
        }

        result.Notes.Insert(0,
            $"Examined {result.BalancesExamined} balance(s) for {year}: "
            + $"{result.BalancesChanged} disagreed with the rulebook, "
            + $"{result.BalancesAlreadyCorrect} were already correct"
            + (result.BalancesFailed > 0 ? $", {result.BalancesFailed} could not be resolved" : string.Empty)
            + "."
            + (result.ChangedWithCarryOverAlreadyRun > 0
                ? $" ⚠ {result.ChangedWithCarryOverAlreadyRun} of the changed rows have a carry-over into {year + 1}"
                  + " that was computed from the old figure and is NOT revisited here."
                : string.Empty)
            + (dryRun ? " NOTHING WAS WRITTEN - this was a dry run." : string.Empty));

        _logger.LogInformation(
            "Entitlement repair {Mode} for {Year}: {Changed} of {Examined} balance(s) changed, {Failed} failed",
            dryRun ? "previewed" : "applied", year,
            result.BalancesChanged, result.BalancesExamined, result.BalancesFailed);

        return result;
    }

    /// <summary>A balance in words, for the notes a person reads before deciding to overwrite.</summary>
    private static string Describe(LeaveBalance balance)
    {
        var who = balance.Employee is { } e
            ? $"{e.FirstName} {e.LastName} ({e.EmployeeNumber})"
            : balance.EmployeeId.ToString();
        var what = balance.LeaveType?.Name ?? balance.LeaveTypeId.ToString();
        return $"{who} · {what}";
    }
}
