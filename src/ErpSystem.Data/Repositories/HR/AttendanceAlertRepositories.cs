using ErpSystem.Core.Entities.HR.StaffAttendance;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Data.Repositories.HR;

// ============================================================================
// STAFF ATTENDANCE ALERT RULE REPOSITORY
// ============================================================================

#region Staff Attendance Alert Rule Repository

public class StaffAttendanceAlertRuleRepository : GenericRepository<StaffAttendanceAlertRule>, IStaffAttendanceAlertRuleRepository
{
    public StaffAttendanceAlertRuleRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<StaffAttendanceAlertRule>> GetActiveRulesAsync()
    {
        return await _dbSet
            .Where(r => r.IsActive && !r.IsDeleted)
            .OrderBy(r => r.TriggerType)
            .ThenBy(r => r.RuleName)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffAttendanceAlertRule>> GetByTriggerTypeAsync(AttendanceAlertTriggerType triggerType)
    {
        return await _dbSet
            .Where(r => r.TriggerType == triggerType && r.IsActive && !r.IsDeleted)
            .OrderBy(r => r.RuleName)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffAttendanceAlertRule>> GetRulesForOrganizationUnitAsync(Guid organizationUnitId)
    {
        return await _dbSet
            .Include(r => r.OrganizationUnit)
            .Where(r => r.IsActive && !r.IsDeleted
                     && (r.OrganizationUnitId == null || r.OrganizationUnitId == organizationUnitId))
            .OrderBy(r => r.TriggerType)
            .ThenBy(r => r.RuleName)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffAttendanceAlertRule>> GetRulesForEmployeeAsync(Guid employeeId)
    {
        return await _dbSet
            .Where(r => r.EmployeeId == employeeId && r.IsActive && !r.IsDeleted)
            .OrderBy(r => r.TriggerType)
            .ToListAsync();
    }

    public async Task<StaffAttendanceAlertRule?> GetWithAlertsAsync(Guid id)
    {
        return await _dbSet
            .Include(r => r.OrganizationUnit)
            .Include(r => r.Position)
            .Include(r => r.Employee)
            .Include(r => r.Alerts.Where(a => !a.IsDeleted))
                .ThenInclude(a => a.Employee)
            .FirstOrDefaultAsync(r => r.Id == id && !r.IsDeleted);
    }
}

#endregion

// ============================================================================
// STAFF ATTENDANCE ALERT REPOSITORY
// ============================================================================

#region Staff Attendance Alert Repository

public class StaffAttendanceAlertRepository : GenericRepository<StaffAttendanceAlert>, IStaffAttendanceAlertRepository
{
    public StaffAttendanceAlertRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<StaffAttendanceAlert>> GetByEmployeeIdAsync(Guid employeeId)
    {
        return await _dbSet
            .Include(a => a.AlertRule)
            .Include(a => a.AcknowledgedBy)
            .Include(a => a.ResolvedBy)
            .Where(a => a.EmployeeId == employeeId && !a.IsDeleted)
            .OrderByDescending(a => a.TriggeredDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffAttendanceAlert>> GetByAlertRuleIdAsync(Guid alertRuleId)
    {
        return await _dbSet
            .Include(a => a.Employee)
            .Where(a => a.AlertRuleId == alertRuleId && !a.IsDeleted)
            .OrderByDescending(a => a.TriggeredDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffAttendanceAlert>> GetByStatusAsync(AttendanceAlertStatus status)
    {
        return await _dbSet
            .Include(a => a.Employee)
            .Include(a => a.AlertRule)
            .Where(a => a.Status == status && !a.IsDeleted)
            .OrderByDescending(a => a.TriggeredDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffAttendanceAlert>> GetActiveUnacknowledgedAlertsAsync()
    {
        return await _dbSet
            .Include(a => a.Employee)
            .Include(a => a.AlertRule)
            .Where(a => a.Status == AttendanceAlertStatus.Active
                     && a.AcknowledgedById == null
                     && !a.IsDeleted)
            .OrderByDescending(a => a.Severity)
            .ThenBy(a => a.TriggeredDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffAttendanceAlert>> GetActiveAlertsForEmployeeAsync(Guid employeeId)
    {
        return await _dbSet
            .Include(a => a.AlertRule)
            .Where(a => a.EmployeeId == employeeId
                     && a.Status == AttendanceAlertStatus.Active
                     && !a.IsDeleted)
            .OrderByDescending(a => a.Severity)
            .ThenBy(a => a.TriggeredDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffAttendanceAlert>> GetBySeverityAsync(AttendanceAlertSeverity severity)
    {
        return await _dbSet
            .Include(a => a.Employee)
            .Include(a => a.AlertRule)
            .Where(a => a.Severity == severity && a.Status == AttendanceAlertStatus.Active && !a.IsDeleted)
            .OrderBy(a => a.TriggeredDate)
            .ToListAsync();
    }

    public async Task<StaffAttendanceAlert?> GetWithFullDetailsAsync(Guid id)
    {
        return await _dbSet
            .Include(a => a.AlertRule)
            .Include(a => a.Employee)
            .Include(a => a.AcknowledgedBy)
            .Include(a => a.ResolvedBy)
            .Include(a => a.DismissedBy)
            .FirstOrDefaultAsync(a => a.Id == id && !a.IsDeleted);
    }
}

#endregion
