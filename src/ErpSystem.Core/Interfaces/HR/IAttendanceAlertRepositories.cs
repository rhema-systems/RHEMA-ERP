using ErpSystem.Core.Entities.HR.StaffAttendance;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Interfaces.HR;

// ============================================================================
// STAFF ATTENDANCE ALERT RULE
// ============================================================================

#region Staff Attendance Alert Rule

public interface IStaffAttendanceAlertRuleRepository : IGenericRepository<StaffAttendanceAlertRule>
{
    /// <summary>Returns all active alert rules.</summary>
    Task<IEnumerable<StaffAttendanceAlertRule>> GetActiveRulesAsync();

    /// <summary>Returns active alert rules filtered by trigger type.</summary>
    Task<IEnumerable<StaffAttendanceAlertRule>> GetByTriggerTypeAsync(AttendanceAlertTriggerType triggerType);

    /// <summary>Returns alert rules scoped to a specific organization unit, including tenant-wide rules (null OrgUnitId).</summary>
    Task<IEnumerable<StaffAttendanceAlertRule>> GetRulesForOrganizationUnitAsync(Guid organizationUnitId);

    /// <summary>Returns alert rules that target a specific employee directly.</summary>
    Task<IEnumerable<StaffAttendanceAlertRule>> GetRulesForEmployeeAsync(Guid employeeId);

    /// <summary>Returns an alert rule fully loaded with its fired alert instances.</summary>
    Task<StaffAttendanceAlertRule?> GetWithAlertsAsync(Guid id);
}

#endregion

// ============================================================================
// STAFF ATTENDANCE ALERT
// ============================================================================

#region Staff Attendance Alert

public interface IStaffAttendanceAlertRepository : IGenericRepository<StaffAttendanceAlert>
{
    /// <summary>Returns all alerts fired for a specific employee, newest first.</summary>
    Task<IEnumerable<StaffAttendanceAlert>> GetByEmployeeIdAsync(Guid employeeId);

    /// <summary>Returns alerts fired by a specific rule.</summary>
    Task<IEnumerable<StaffAttendanceAlert>> GetByAlertRuleIdAsync(Guid alertRuleId);

    /// <summary>Returns alerts filtered by lifecycle status.</summary>
    Task<IEnumerable<StaffAttendanceAlert>> GetByStatusAsync(AttendanceAlertStatus status);

    /// <summary>Returns all currently active alerts (Status = Active) that have not yet been acknowledged.</summary>
    Task<IEnumerable<StaffAttendanceAlert>> GetActiveUnacknowledgedAlertsAsync();

    /// <summary>Returns active alerts for a specific employee that require acknowledgement.</summary>
    Task<IEnumerable<StaffAttendanceAlert>> GetActiveAlertsForEmployeeAsync(Guid employeeId);

    /// <summary>Returns alerts filtered by severity level.</summary>
    Task<IEnumerable<StaffAttendanceAlert>> GetBySeverityAsync(AttendanceAlertSeverity severity);

    /// <summary>Returns a fully-loaded alert including rule, employee, and audit trail navigations.</summary>
    Task<StaffAttendanceAlert?> GetWithFullDetailsAsync(Guid id);
}

#endregion
