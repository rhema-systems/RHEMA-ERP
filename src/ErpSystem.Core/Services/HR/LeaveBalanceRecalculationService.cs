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
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<LeaveBalanceRecalculationService> _logger;

    public LeaveBalanceRecalculationService(
        IGenericRepository<LeaveBalance> leaveBalanceRepository,
        IGenericRepository<LeaveRequest> leaveRequestRepository,
        IGenericRepository<LeaveAdjustment> leaveAdjustmentRepository,
        IGenericRepository<LeaveEncashment> leaveEncashmentRepository,
        IGenericRepository<LeaveType> leaveTypeRepository,
        ILeaveEntitlementService entitlementService,
        IUnitOfWork unitOfWork,
        ILogger<LeaveBalanceRecalculationService> logger)
    {
        _leaveBalanceRepository = leaveBalanceRepository;
        _leaveRequestRepository = leaveRequestRepository;
        _leaveAdjustmentRepository = leaveAdjustmentRepository;
        _leaveEncashmentRepository = leaveEncashmentRepository;
        _leaveTypeRepository = leaveTypeRepository;
        _entitlementService = entitlementService;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task RecalculateAsync(Guid employeeId, Guid leaveTypeId, int year)
    {
        var balance = await _leaveBalanceRepository
            .GetQueryable()
            .FirstOrDefaultAsync(lb =>
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

        // UsedDays — sum of approved requests
        balance.UsedDays = await _leaveRequestRepository
            .GetQueryable()
            .Where(r =>
                r.EmployeeId  == employeeId &&
                r.LeaveTypeId == leaveTypeId &&
                r.StartDate.Year == year &&
                r.Status == LeaveStatus.Approved)
            .SumAsync(r => (decimal?)r.TotalDays) ?? 0m;

        // PendingDays — sum of pending / in-progress requests
        balance.PendingDays = await _leaveRequestRepository
            .GetQueryable()
            .Where(r =>
                r.EmployeeId  == employeeId &&
                r.LeaveTypeId == leaveTypeId &&
                r.StartDate.Year == year &&
                (r.Status == LeaveStatus.Pending || r.Status == LeaveStatus.InProgress))
            .SumAsync(r => (decimal?)r.TotalDays) ?? 0m;

        // AdjustmentDays — sum of all adjustments linked to this balance
        balance.AdjustmentDays = await _leaveAdjustmentRepository
            .GetQueryable()
            .Where(a =>
                a.EmployeeId  == employeeId &&
                a.LeaveTypeId == leaveTypeId &&
                a.Year        == year)
            .SumAsync(a => (decimal?)a.Days) ?? 0m;

        // EncashedDays — sum of processed encashments (single source of truth; the encashment
        // service no longer mutates UsedDays directly, so a recalc can't erase the deduction).
        balance.EncashedDays = await _leaveEncashmentRepository
            .GetQueryable()
            .Where(e =>
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
        var leaveTypeIds = await _leaveBalanceRepository
            .GetQueryable()
            .Where(lb => lb.EmployeeId == employeeId && lb.Year == year)
            .Select(lb => lb.LeaveTypeId)
            .Distinct()
            .ToListAsync();

        foreach (var leaveTypeId in leaveTypeIds)
        {
            await RecalculateAsync(employeeId, leaveTypeId, year);
        }
    }
}
