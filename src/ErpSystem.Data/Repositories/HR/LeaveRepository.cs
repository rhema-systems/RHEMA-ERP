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
            .Include(lr => lr.LeaveSubType)
            .Include(lr => lr.RelieverEmployee)
            .FirstOrDefaultAsync(la => la.RequestNumber == requestNumber);
    }

    public async Task<IEnumerable<LeaveRequest>> GetEmployeeLeaveHistoryAsync(Guid employeeId, int year)
    {
        return await _dbSet
            .Include(lr => lr.LeaveType)
            .Include(lr => lr.LeaveSubType)
            .Where(lr => lr.EmployeeId == employeeId && lr.StartDate.Year == year)
            .OrderByDescending(lr => lr.StartDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<LeaveRequest>> GetPendingApprovalsForManagerAsync(Guid managerId)
    {
        var reportingEmployeeIds = await _context.Employees
            .Where(e => e.ManagerId == managerId)
            .Select(e => e.Id)
            .ToListAsync();

        return await _dbSet
            .Include(lr => lr.Employee)
            .Include(lr => lr.LeaveType)
            .Include(lr => lr.LeaveSubType)
            .Include(lr => lr.RelieverEmployee)
            .Where(lr => reportingEmployeeIds.Contains(lr.EmployeeId) && lr.Status == LeaveStatus.Pending)
            .OrderBy(la => la.RequestDate)
            .ToListAsync();
    }

    public async Task<bool> HasConflictingLeaveAsync(Guid employeeId, DateOnly startDate, DateOnly endDate,
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

    public async Task<bool> RelieverHasConflictAsync(Guid relieverId, DateOnly startDate, DateOnly endDate)
    {
        var relieverOnLeave = await _dbSet
            .AnyAsync(la => la.EmployeeId == relieverId &&
                           la.Status == LeaveStatus.Approved &&
                           la.StartDate <= endDate &&
                           la.EndDate >= startDate);

        if (relieverOnLeave)
            return true;

        var relieverAlreadyAssigned = await _dbSet
            .AnyAsync(la => la.RelieverEmployeeId == relieverId &&
                           la.Status == LeaveStatus.Approved &&
                           la.StartDate <= endDate &&
                           la.EndDate >= startDate);

        return relieverAlreadyAssigned;
    }

    public async Task<IEnumerable<LeaveRequest>> GetActiveLeaveRequestsAsync(DateOnly? asOfDate = null)
    {
        var targetDate = asOfDate ?? DateOnly.FromDateTime(DateTime.Today);

        return await _dbSet.Include(la => la.Employee)
                           .Include(la => la.LeaveType)
                           .Where(la => la.Status == LeaveStatus.Approved && la.StartDate <= targetDate
                                    && la.EndDate >= targetDate)
                           .ToListAsync();
    }
}

public class LeaveTypeRepository : GenericRepository<LeaveType>, ILeaveTypeRepository
{
    public LeaveTypeRepository(ApplicationDbContext context) : base(context) { }

    public async Task<LeaveType?> GetByCodeAsync(string code)
        => await _dbSet.FirstOrDefaultAsync(lt => lt.Code == code);

    public async Task<LeaveType?> GetWithSubTypesAsync(Guid id)
        => await _dbSet
            .Include(lt => lt.LeaveSubTypes)
            .FirstOrDefaultAsync(lt => lt.Id == id);

    public async Task<LeaveType?> GetWithPoliciesAsync(Guid id)
        => await _dbSet
            .Include(lt => lt.EligibilityRules)
            .Include(lt => lt.AccrualPolicies)
            .Include(lt => lt.LeaveCategoryAllocations).ThenInclude(a => a.StaffLevel)
            .FirstOrDefaultAsync(lt => lt.Id == id);

    public async Task<IEnumerable<LeaveType>> GetActiveLeaveTypesAsync()
        => await _dbSet.Where(lt => lt.IsActive).OrderBy(lt => lt.Name).ToListAsync();
}

public class LeavePlanRepository : GenericRepository<LeavePlan>, ILeavePlanRepository
{
    public LeavePlanRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<LeavePlan>> GetByEmployeeAndYearAsync(Guid employeeId, int year)
        => await _dbSet
            .Include(p => p.Employee)
            .Include(p => p.LeaveType)
            .Include(p => p.LeaveSubType)
            .Include(p => p.RelieverEmployee)
            .Include(p => p.PlannedByEmployee)
            .Where(p => p.EmployeeId == employeeId && p.Year == year)
            .OrderBy(p => p.StartDate)
            .ToListAsync();

    public async Task<IEnumerable<LeavePlan>> GetByYearAsync(int year)
        => await _dbSet
            .Include(p => p.Employee)
            .Include(p => p.LeaveType)
            .Include(p => p.LeaveSubType)
            .Include(p => p.RelieverEmployee)
            .Include(p => p.PlannedByEmployee)
            .Include(p => p.OrganizationUnit)
            .Where(p => p.Year == year)
            .OrderBy(p => p.StartDate)
            .ToListAsync();

    public async Task<bool> HasConflictingPlanAsync(Guid employeeId, DateOnly startDate, DateOnly endDate, Guid? excludePlanId = null)
    {
        var query = _dbSet.Where(p =>
            p.EmployeeId == employeeId &&
            p.Status != LeavePlanStatus.Cancelled &&
            p.Status != LeavePlanStatus.Rejected &&
            p.StartDate <= endDate &&
            p.EndDate >= startDate);

        if (excludePlanId.HasValue)
            query = query.Where(p => p.Id != excludePlanId.Value);

        return await query.AnyAsync();
    }
}

public class LeaveBalanceRepository : GenericRepository<LeaveBalance>, ILeaveBalanceRepository
{
    public LeaveBalanceRepository(ApplicationDbContext context) : base(context) { }

    public async Task<LeaveBalance?> GetBalanceAsync(Guid employeeId, Guid leaveTypeId, Guid? leaveSubTypeId, int year)
        => await _dbSet
            .Include(b => b.LeaveType)
            .Include(b => b.LeaveSubType)
            .FirstOrDefaultAsync(b =>
                b.EmployeeId == employeeId &&
                b.LeaveTypeId == leaveTypeId &&
                b.LeaveSubTypeId == leaveSubTypeId &&
                b.Year == year);

    public async Task<IEnumerable<LeaveBalance>> GetEmployeeBalancesAsync(Guid employeeId, int year)
        => await _dbSet
            .Include(b => b.LeaveType)
            .Include(b => b.LeaveSubType)
            .Where(b => b.EmployeeId == employeeId && b.Year == year)
            .OrderBy(b => b.LeaveType.Name)
            .ToListAsync();
}
