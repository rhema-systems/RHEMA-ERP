using ErpSystem.Core.Entities.HR.StaffLeave;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Data.Repositories.HR;

public class LeaveRepository : GenericRepository<LeaveRequest>, ILeaveRepository
{
    public LeaveRepository(ApplicationDbContext context) : base(context)
    {
    }

    public async Task<LeaveRequest?> GetByRequestNumberAsync(string requestNumber)
    {
        return await _dbSet
            .Include(lr => lr.Employee)
            .Include(lr => lr.LeaveType)
            .Include(lr => lr.RelieverEmployee)
            .Include(lr => lr.ApprovedByEmployee)
            .FirstOrDefaultAsync(la => la.RequestNumber == requestNumber);
    }

    public async Task<IEnumerable<LeaveRequest>> GetEmployeeLeaveHistoryAsync(Guid employeeId, int year)
    {
        return await _dbSet
            .Include(lr => lr.LeaveType)
            .Where(lr => lr.EmployeeId == employeeId && lr.StartDate.Year == year)
            .OrderByDescending(lr => lr.StartDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<LeaveRequest>> GetPendingApprovalsForManagerAsync(Guid managerId)
    {
        // Get employees reporting to this manager
        var reportingEmployeeIds = await _context.Employees
            .Where(e => e.ManagerId == managerId)
            .Select(e => e.Id)
            .ToListAsync();

        return await _dbSet
            .Include(lr => lr.Employee)
            .Include(lr => lr.LeaveType)
            .Include(lr => lr.RelieverEmployee)
            .Where(lr => reportingEmployeeIds.Contains(lr.EmployeeId) && lr.Status == LeaveStatus.Pending)
            .OrderBy(la => la.RequestDate)
            .ToListAsync();
    }

    public async Task<bool> HasConflictingLeaveAsync(Guid employeeId, DateTime startDate, DateTime endDate,
        Guid? excludeRequestId = null)
    {
        var query = _dbSet
            .Where(la => la.EmployeeId == employeeId &&
                        (la.Status == LeaveStatus.Approved || la.Status == LeaveStatus.Pending) &&
                        (la.StartDate <= endDate && la.EndDate >= startDate));

        if (excludeRequestId.HasValue)
        {
            query = query.Where(la => la.Id != excludeRequestId.Value);
        }

        return await query.AnyAsync();
    }

    public async Task<bool> RelieverHasConflictAsync(Guid relieverId, DateTime startDate, DateTime endDate)
    {
        // Check if reliever is on leave during this period
        var relieverOnLeave = await _dbSet
            .AnyAsync(la => la.EmployeeId == relieverId &&
                           la.Status == LeaveStatus.Approved &&
                           la.StartDate <= endDate &&
                           la.EndDate >= startDate);

        if (relieverOnLeave)
            return true;

        // Check if reliever is already assigned as a reliever during this period
        var relieverAlreadyAssigned = await _dbSet
            .AnyAsync(la => la.RelieverEmployeeId == relieverId &&
                           la.Status == LeaveStatus.Approved &&
                           la.StartDate <= endDate &&
                           la.EndDate >= startDate);

        return relieverAlreadyAssigned;
    }

    public async Task<IEnumerable<LeaveRequest>> GetActiveLeaveRequestsAsync(DateTime? asOfDate = null)
    {
        var targetDate = asOfDate ?? DateTime.Today;

        return await _dbSet.Include(la => la.Employee)
                           .Include(la => la.LeaveType)
                           .Where(la => la.Status == LeaveStatus.Approved && la.StartDate <= targetDate
                                    && la.EndDate >= targetDate)
                           .ToListAsync();
    }
}