using ErpSystem.Core.Entities.HR.StaffLeave;

namespace ErpSystem.Core.Interfaces.HR;

#region Leave Management Repositories

public interface ILeaveRepository : IGenericRepository<LeaveRequest>
{
    Task<LeaveRequest?> GetByRequestNumberAsync(string requestNumber);
    Task<IEnumerable<LeaveRequest>> GetEmployeeLeaveHistoryAsync(Guid employeeId, int year);
    Task<IEnumerable<LeaveRequest>> GetPendingApprovalsForManagerAsync(Guid managerId);
    Task<bool> HasConflictingLeaveAsync(Guid employeeId, DateOnly startDate, DateOnly endDate, Guid? excludeRequestId = null);
    Task<bool> RelieverHasConflictAsync(Guid relieverId, DateOnly startDate, DateOnly endDate);
    Task<IEnumerable<LeaveRequest>> GetActiveLeaveRequestsAsync(DateOnly? asOfDate = null);
}

public interface ILeaveTypeRepository : IGenericRepository<LeaveType>
{
    Task<LeaveType?> GetByCodeAsync(string code);
    Task<LeaveType?> GetWithSubTypesAsync(Guid id);
    Task<LeaveType?> GetWithPoliciesAsync(Guid id);
    Task<IEnumerable<LeaveType>> GetActiveLeaveTypesAsync();
}

public interface ILeavePlanRepository : IGenericRepository<LeavePlan>
{
    Task<IEnumerable<LeavePlan>> GetByEmployeeAndYearAsync(Guid employeeId, int year);
    Task<IEnumerable<LeavePlan>> GetByYearAsync(int year);
    Task<bool> HasConflictingPlanAsync(Guid employeeId, DateOnly startDate, DateOnly endDate, Guid? excludePlanId = null);
}

public interface ILeaveBalanceRepository : IGenericRepository<LeaveBalance>
{
    Task<LeaveBalance?> GetBalanceAsync(Guid employeeId, Guid leaveTypeId, Guid? leaveSubTypeId, int year);
    Task<IEnumerable<LeaveBalance>> GetEmployeeBalancesAsync(Guid employeeId, int year);
}

#endregion Leave Management Repositories
