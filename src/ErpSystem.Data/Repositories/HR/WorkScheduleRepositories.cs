using ErpSystem.Core.Entities.HR.StaffAttendance;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Data.Repositories.HR;

// ============================================================================
// WORK SCHEDULE REPOSITORY
// ============================================================================

#region Work Schedule Repository

public class WorkScheduleRepository : GenericRepository<WorkSchedule>, IWorkScheduleRepository
{
    public WorkScheduleRepository(ApplicationDbContext context) : base(context) { }

    public async Task<WorkSchedule?> GetDefaultScheduleAsync()
    {
        return await _dbSet
            .Include(s => s.Shifts)
            .FirstOrDefaultAsync(s => s.IsDefault && s.IsActive && !s.IsDeleted);
    }

    public async Task<IEnumerable<WorkSchedule>> GetActiveSchedulesAsync()
    {
        return await _dbSet
            .Where(s => s.IsActive && !s.IsDeleted)
            .OrderBy(s => s.ScheduleName)
            .ToListAsync();
    }

    public async Task<IEnumerable<WorkSchedule>> GetByTypeAsync(WorkScheduleType type)
    {
        return await _dbSet
            .Where(s => s.Type == type && !s.IsDeleted)
            .OrderBy(s => s.ScheduleName)
            .ToListAsync();
    }

    public async Task<WorkSchedule?> GetWithShiftsAsync(Guid id)
    {
        return await _dbSet
            .Include(s => s.Shifts.Where(sh => !sh.IsDeleted))
            .FirstOrDefaultAsync(s => s.Id == id && !s.IsDeleted);
    }
}

#endregion

// ============================================================================
// EMPLOYEE WORK SCHEDULE REPOSITORY
// ============================================================================

#region Employee Work Schedule Repository

public class EmployeeWorkScheduleRepository : GenericRepository<EmployeeWorkSchedule>, IEmployeeWorkScheduleRepository
{
    public EmployeeWorkScheduleRepository(ApplicationDbContext context) : base(context) { }

    public async Task<EmployeeWorkSchedule?> GetCurrentForEmployeeAsync(Guid employeeId)
    {
        return await _dbSet
            .Include(e => e.WorkSchedule)
            .Include(e => e.AssignedBy)
            .FirstOrDefaultAsync(e => e.EmployeeId == employeeId && e.IsCurrent && !e.IsDeleted);
    }

    public async Task<IEnumerable<EmployeeWorkSchedule>> GetAllForEmployeeAsync(Guid employeeId)
    {
        return await _dbSet
            .Include(e => e.WorkSchedule)
            .Include(e => e.AssignedBy)
            .Where(e => e.EmployeeId == employeeId && !e.IsDeleted)
            .OrderByDescending(e => e.EffectiveDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<EmployeeWorkSchedule>> GetByWorkScheduleIdAsync(Guid workScheduleId)
    {
        return await _dbSet
            .Include(e => e.Employee)
            .Where(e => e.WorkScheduleId == workScheduleId && !e.IsDeleted)
            .OrderBy(e => e.Employee.LastName)
            .ThenBy(e => e.Employee.FirstName)
            .ToListAsync();
    }

    public async Task<(IEnumerable<EmployeeWorkSchedule> Items, int TotalCount)> GetPagedWithCountAsync(int pageNumber, int pageSize)
    {
        var query = _dbSet
            .Include(e => e.Employee)
            .Include(e => e.WorkSchedule)
            .Include(e => e.AssignedBy)
            .Where(e => !e.IsDeleted);

        var totalCount = await query.CountAsync();

        var items = await query
            .OrderByDescending(e => e.EffectiveDate)
            .ThenBy(e => e.Employee.LastName)
            .ThenBy(e => e.Employee.FirstName)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, totalCount);
    }

    public async Task<IEnumerable<EmployeeWorkSchedule>> GetActiveAssignmentsAsOfAsync(DateOnly asOfDate)
    {
        return await _dbSet
            .Include(e => e.Employee)
            .Include(e => e.WorkSchedule)
            .Where(e => e.EffectiveDate <= asOfDate
                     && (e.EndDate == null || e.EndDate >= asOfDate)
                     && !e.IsDeleted)
            .OrderBy(e => e.Employee.LastName)
            .ToListAsync();
    }
}

#endregion

// ============================================================================
// SHIFT DEFINITION REPOSITORY
// ============================================================================

#region Shift Definition Repository

public class ShiftDefinitionRepository : GenericRepository<ShiftDefinition>, IShiftDefinitionRepository
{
    public ShiftDefinitionRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<ShiftDefinition>> GetByWorkScheduleIdAsync(Guid workScheduleId)
    {
        return await _dbSet
            .Include(s => s.WorkSchedule)
            .Where(s => s.WorkScheduleId == workScheduleId && !s.IsDeleted)
            .OrderBy(s => s.DisplayOrder)
            .ToListAsync();
    }

    public async Task<IEnumerable<ShiftDefinition>> GetActiveShiftsAsync()
    {
        return await _dbSet
            .Include(s => s.WorkSchedule)
            .Where(s => s.IsActive && !s.IsDeleted)
            .OrderBy(s => s.WorkSchedule.ScheduleName)
            .ThenBy(s => s.DisplayOrder)
            .ToListAsync();
    }

    public async Task<IEnumerable<ShiftDefinition>> GetNightShiftsAsync()
    {
        return await _dbSet
            .Include(s => s.WorkSchedule)
            .Where(s => s.IsNightShift && s.IsActive && !s.IsDeleted)
            .OrderBy(s => s.WorkSchedule.ScheduleName)
            .ThenBy(s => s.DisplayOrder)
            .ToListAsync();
    }

    public async Task<IEnumerable<ShiftDefinition>> GetByTypeAsync(ShiftType type)
    {
        return await _dbSet
            .Include(s => s.WorkSchedule)
            .Where(s => s.Type == type && !s.IsDeleted)
            .OrderBy(s => s.WorkSchedule.ScheduleName)
            .ThenBy(s => s.DisplayOrder)
            .ToListAsync();
    }

    public async Task<IEnumerable<ShiftDefinition>> GetAllAsync()
    {
        return await _dbSet
            .Include(s => s.WorkSchedule)
            .Where(s => !s.IsDeleted)
            .OrderBy(s => s.WorkSchedule.ScheduleName)
            .ThenBy(s => s.DisplayOrder)
            .ToListAsync();
    }
}

#endregion

// ============================================================================
// SHIFT ASSIGNMENT REPOSITORY
// ============================================================================

#region Shift Assignment Repository

public class ShiftAssignmentRepository : GenericRepository<ShiftAssignment>, IShiftAssignmentRepository
{
    public ShiftAssignmentRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<ShiftAssignment>> GetByEmployeeIdAsync(Guid employeeId)
    {
        return await _dbSet
            .Include(a => a.ShiftDefinition).ThenInclude(s => s.WorkSchedule)
            .Include(a => a.AssignedBy)
            .Where(a => a.EmployeeId == employeeId && !a.IsDeleted)
            .OrderByDescending(a => a.AssignmentDate)
            .ToListAsync();
    }

    public async Task<ShiftAssignment?> GetCurrentAssignmentForEmployeeAsync(Guid employeeId)
    {
        var now = DateTime.UtcNow;
        return await _dbSet
            .Include(a => a.ShiftDefinition).ThenInclude(s => s.WorkSchedule)
            .Where(a => a.EmployeeId == employeeId
                     && a.AssignmentDate <= now
                     && (a.EndDate == null || a.EndDate >= now)
                     && !a.IsDeleted)
            .OrderByDescending(a => a.AssignmentDate)
            .FirstOrDefaultAsync();
    }

    public async Task<IEnumerable<ShiftAssignment>> GetByShiftDefinitionIdAsync(Guid shiftDefinitionId)
    {
        return await _dbSet
            .Include(a => a.Employee)
            .Include(a => a.AssignedBy)
            .Where(a => a.ShiftDefinitionId == shiftDefinitionId && !a.IsDeleted)
            .OrderBy(a => a.Employee.LastName)
            .ThenBy(a => a.Employee.FirstName)
            .ToListAsync();
    }

    public async Task<IEnumerable<ShiftAssignment>> GetActiveAssignmentsAsync(DateTime? asOf = null)
    {
        var cutoff = asOf ?? DateTime.UtcNow;
        return await _dbSet
            .Include(a => a.Employee)
            .Include(a => a.ShiftDefinition).ThenInclude(s => s.WorkSchedule)
            .Where(a => a.AssignmentDate <= cutoff
                     && (a.EndDate == null || a.EndDate >= cutoff)
                     && !a.IsDeleted)
            .OrderBy(a => a.Employee.LastName)
            .ToListAsync();
    }
}

#endregion

// ============================================================================
// SHIFT ROTATION PLAN REPOSITORY
// ============================================================================

#region Shift Rotation Plan Repository

public class ShiftRotationPlanRepository : GenericRepository<ShiftRotationPlan>, IShiftRotationPlanRepository
{
    public ShiftRotationPlanRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<ShiftRotationPlan>> GetActivePlansAsync()
    {
        return await _dbSet
            .Where(p => p.IsActive && !p.IsDeleted)
            .OrderBy(p => p.PlanName)
            .ToListAsync();
    }

    public async Task<IEnumerable<ShiftRotationPlan>> GetByRotationCycleAsync(ShiftRotationCycle cycle)
    {
        return await _dbSet
            .Where(p => p.RotationCycle == cycle && !p.IsDeleted)
            .OrderBy(p => p.PlanName)
            .ToListAsync();
    }

    public async Task<ShiftRotationPlan?> GetWithStagesAsync(Guid id)
    {
        return await _dbSet
            .Include(p => p.Stages).ThenInclude(s => s.ShiftDefinition)
            .FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted);
    }

    public async Task<ShiftRotationPlan?> GetWithMembersAsync(Guid id)
    {
        return await _dbSet
            .Include(p => p.Stages).ThenInclude(s => s.ShiftDefinition)
            .Include(p => p.Members.Where(m => m.ExitDate == null && !m.IsDeleted))
                .ThenInclude(m => m.Employee)
            .FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted);
    }
}

#endregion

// ============================================================================
// SHIFT ROTATION STAGE REPOSITORY
// ============================================================================

#region Shift Rotation Stage Repository

public class ShiftRotationStageRepository : GenericRepository<ShiftRotationStage>, IShiftRotationStageRepository
{
    public ShiftRotationStageRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<ShiftRotationStage>> GetByPlanIdAsync(Guid planId)
    {
        return await _dbSet
            .Include(s => s.ShiftDefinition)
            .Where(s => s.ShiftRotationPlanId == planId && !s.IsDeleted)
            .OrderBy(s => s.StageOrder)
            .ToListAsync();
    }

    public async Task<IEnumerable<ShiftRotationStage>> GetByShiftDefinitionIdAsync(Guid shiftDefinitionId)
    {
        return await _dbSet
            .Include(s => s.ShiftRotationPlan)
            .Where(s => s.ShiftDefinitionId == shiftDefinitionId && !s.IsDeleted)
            .OrderBy(s => s.ShiftRotationPlan.PlanName)
            .ThenBy(s => s.StageOrder)
            .ToListAsync();
    }
}

#endregion

// ============================================================================
// SHIFT ROTATION MEMBER REPOSITORY
// ============================================================================

#region Shift Rotation Member Repository

public class ShiftRotationMemberRepository : GenericRepository<ShiftRotationMember>, IShiftRotationMemberRepository
{
    public ShiftRotationMemberRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<ShiftRotationMember>> GetByPlanIdAsync(Guid planId)
    {
        return await _dbSet
            .Include(m => m.Employee)
            .Include(m => m.OrganizationUnit)
            .Include(m => m.Team)
            .Where(m => m.ShiftRotationPlanId == planId && m.ExitDate == null && !m.IsDeleted)
            .OrderBy(m => m.Employee!.LastName)
            .ToListAsync();
    }

    public async Task<IEnumerable<ShiftRotationMember>> GetByEmployeeIdAsync(Guid employeeId)
    {
        return await _dbSet
            .Include(m => m.ShiftRotationPlan)
            .Where(m => m.EmployeeId == employeeId && !m.IsDeleted)
            .OrderByDescending(m => m.JoinDate)
            .ToListAsync();
    }

    public async Task<ShiftRotationMember?> GetCurrentPlanForEmployeeAsync(Guid employeeId)
    {
        return await _dbSet
            .Include(m => m.ShiftRotationPlan)
                .ThenInclude(p => p.Stages)
                    .ThenInclude(s => s.ShiftDefinition)
            .FirstOrDefaultAsync(m => m.EmployeeId == employeeId && m.ExitDate == null && !m.IsDeleted);
    }

    public async Task<IEnumerable<ShiftRotationMember>> GetByOrganizationUnitIdAsync(Guid planId, Guid organizationUnitId)
    {
        return await _dbSet
            .Include(m => m.Employee)
            .Include(m => m.OrganizationUnit)
            .Where(m => m.ShiftRotationPlanId == planId
                     && m.OrganizationUnitId == organizationUnitId
                     && m.ExitDate == null
                     && !m.IsDeleted)
            .OrderBy(m => m.Employee!.LastName)
            .ToListAsync();
    }
}

#endregion
