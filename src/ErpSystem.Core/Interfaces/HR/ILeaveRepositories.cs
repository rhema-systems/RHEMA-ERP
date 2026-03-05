using ErpSystem.Core.Entities.HR.StaffLeave;

namespace ErpSystem.Core.Interfaces.HR;

#region Leave Management Repositories

public interface ILeaveRepository : IGenericRepository<LeaveRequest>
{
    Task<LeaveRequest?> GetByRequestNumberAsync(string requestNumber);
    Task<IEnumerable<LeaveRequest>> GetEmployeeLeaveHistoryAsync(Guid employeeId, int year);
    Task<IEnumerable<LeaveRequest>> GetPendingApprovalsForManagerAsync(Guid managerId);
    Task<bool> HasConflictingLeaveAsync(Guid employeeId, DateTime startDate, DateTime endDate, Guid? excludeRequestId = null);
    Task<bool> RelieverHasConflictAsync(Guid relieverId, DateTime startDate, DateTime endDate);
    Task<IEnumerable<LeaveRequest>> GetActiveLeaveRequestsAsync(DateTime? asOfDate = null);
}

#endregion Leave Management Repositories