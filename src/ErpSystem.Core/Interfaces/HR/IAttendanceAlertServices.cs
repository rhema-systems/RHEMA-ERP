using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Interfaces.HR;

// ============================================================================
// STAFF ATTENDANCE ALERT RULE SERVICE
// ============================================================================

#region Staff Attendance Alert Rule Service

public interface IStaffAttendanceAlertRuleService
{
    Task<StaffAttendanceAlertRuleDto> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IEnumerable<StaffAttendanceAlertRuleSummaryDto>> GetAllAsync(CancellationToken ct = default);
    Task<IEnumerable<StaffAttendanceAlertRuleSummaryDto>> GetActiveRulesAsync(CancellationToken ct = default);
    Task<IEnumerable<StaffAttendanceAlertRuleSummaryDto>> GetByTypeAsync(AttendanceAlertTriggerType alertType, CancellationToken ct = default);
    Task<PagedResult<StaffAttendanceAlertRuleSummaryDto>> GetPagedAsync(int pageNumber, int pageSize, CancellationToken ct = default);

    Task<StaffAttendanceAlertRuleDto> CreateAsync(CreateStaffAttendanceAlertRuleDto dto, Guid tenantId, Guid userId, CancellationToken ct = default);
    Task<StaffAttendanceAlertRuleDto> UpdateAsync(UpdateStaffAttendanceAlertRuleDto dto, Guid userId, CancellationToken ct = default);
    Task<bool> ToggleActiveAsync(Guid ruleId, Guid userId, CancellationToken ct = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken ct = default);
}

#endregion

// ============================================================================
// STAFF ATTENDANCE ALERT SERVICE
// ============================================================================

#region Staff Attendance Alert Service

public interface IStaffAttendanceAlertService
{
    Task<StaffAttendanceAlertDto> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IEnumerable<StaffAttendanceAlertSummaryDto>> GetByEmployeeIdAsync(Guid employeeId, CancellationToken ct = default);
    Task<IEnumerable<StaffAttendanceAlertSummaryDto>> GetByRuleIdAsync(Guid ruleId, CancellationToken ct = default);
    Task<IEnumerable<StaffAttendanceAlertSummaryDto>> GetUnacknowledgedAlertsAsync(CancellationToken ct = default);
    Task<IEnumerable<StaffAttendanceAlertSummaryDto>> GetUnacknowledgedForEmployeeAsync(Guid employeeId, CancellationToken ct = default);
    Task<IEnumerable<StaffAttendanceAlertSummaryDto>> GetByAttendanceDateAsync(DateOnly date, CancellationToken ct = default);
    Task<IEnumerable<StaffAttendanceAlertSummaryDto>> GetByTypeAsync(AttendanceAlertTriggerType alertType, CancellationToken ct = default);
    Task<IEnumerable<StaffAttendanceAlertSummaryDto>> GetBySeverityAsync(AttendanceAlertSeverity severity, CancellationToken ct = default);
    Task<PagedResult<StaffAttendanceAlertSummaryDto>> GetPagedAsync(int pageNumber, int pageSize, CancellationToken ct = default);

    /// <summary>Evaluates all active alert rules against the given daily attendance record and fires alerts as needed.</summary>
    Task<IEnumerable<StaffAttendanceAlertDto>> EvaluateForAttendanceAsync(Guid dailyAttendanceId, Guid userId, CancellationToken ct = default);

    Task<StaffAttendanceAlertDto> AcknowledgeAsync(Guid alertId, string? comments, Guid userId, CancellationToken ct = default);
    Task<int> BulkAcknowledgeAsync(IEnumerable<Guid> alertIds, string? comments, Guid userId, CancellationToken ct = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken ct = default);
}

#endregion
