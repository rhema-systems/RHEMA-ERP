using ErpSystem.Core.Entities.HR.StaffLeave;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
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
                r.StartDate.Year == year &&
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
                r.StartDate.Year == year &&
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

        // EncashedDays — sum of processed encashments (single source of truth; the encashment
        // service no longer mutates UsedDays directly, so a recalc can't erase the deduction).
        balance.EncashedDays = await _leaveEncashmentRepository
            .GetQueryable()
            .Where(e =>
                e.TenantId == tenantId &&
                e.EmployeeId  == employeeId &&
                e.LeaveTypeId == leaveTypeId &&
                e.Year        == year &&
                e.Status == LeaveEncashmentStatus.Processed)
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
}
