using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Interfaces.HR;

// ============================================================================
// STAFF ATTENDANCE RECORD SERVICE
// ============================================================================

#region Staff Attendance Record Service

public interface IStaffAttendanceRecordService
{
    Task<StaffAttendanceRecordDto> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<StaffAttendanceRecordDto?> GetByEmployeeAndDateAsync(Guid employeeId, DateOnly date, CancellationToken ct = default);
    Task<IEnumerable<StaffAttendanceRecordSummaryDto>> GetByEmployeeIdAsync(Guid employeeId, DateOnly from, DateOnly to, CancellationToken ct = default);
    Task<IEnumerable<StaffAttendanceRecordSummaryDto>> GetByDateAsync(DateOnly date, CancellationToken ct = default);
    Task<IEnumerable<StaffAttendanceRecordSummaryDto>> GetByDateRangeAsync(DateOnly from, DateOnly to, CancellationToken ct = default);
    Task<IEnumerable<StaffAttendanceRecordSummaryDto>> GetByStatusAsync(StaffAttendanceStatus status, DateOnly from, DateOnly to, CancellationToken ct = default);
    Task<PagedResult<StaffAttendanceRecordSummaryDto>> GetPagedAsync(int pageNumber, int pageSize, CancellationToken ct = default);

    Task<StaffAttendanceRecordDto> CreateAsync(CreateStaffAttendanceRecordDto dto, Guid tenantId, Guid userId, CancellationToken ct = default);
    Task<StaffAttendanceRecordDto> UpdateAsync(UpdateStaffAttendanceRecordDto dto, Guid userId, CancellationToken ct = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken ct = default);
}

#endregion

// ============================================================================
// STAFF DAILY ATTENDANCE SERVICE
// ============================================================================

#region Staff Daily Attendance Service

public interface IStaffDailyAttendanceService
{
    Task<StaffDailyAttendanceDto> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<StaffDailyAttendanceDto?> GetByEmployeeAndDateAsync(Guid employeeId, DateOnly date, CancellationToken ct = default);
    Task<IEnumerable<StaffDailyAttendanceSummaryDto>> GetByEmployeeIdAsync(Guid employeeId, DateOnly from, DateOnly to, CancellationToken ct = default);
    Task<IEnumerable<StaffDailyAttendanceSummaryDto>> GetByDateAsync(DateOnly date, CancellationToken ct = default);
    Task<IEnumerable<StaffDailyAttendanceSummaryDto>> GetByStatusAsync(StaffAttendanceStatus status, DateOnly from, DateOnly to, CancellationToken ct = default);
    Task<IEnumerable<StaffDailyAttendanceSummaryDto>> GetPendingVerificationAsync(CancellationToken ct = default);
    Task<IEnumerable<StaffDailyAttendanceSummaryDto>> GetWithOpenExceptionsAsync(CancellationToken ct = default);
    Task<IEnumerable<StaffDailyAttendanceSummaryDto>> GetWithOvertimeAsync(DateOnly from, DateOnly to, Guid? employeeId = null, CancellationToken ct = default);
    Task<IEnumerable<StaffDailyAttendanceSummaryDto>> GetLateAttendancesAsync(DateOnly from, DateOnly to, Guid? employeeId = null, CancellationToken ct = default);
    Task<IEnumerable<StaffDailyAttendanceSummaryDto>> GetRemoteWorkDaysAsync(DateOnly from, DateOnly to, Guid? employeeId = null, CancellationToken ct = default);
    Task<IEnumerable<StaffDailyAttendanceSummaryDto>> GetByPayPeriodIdAsync(Guid payPeriodId, CancellationToken ct = default);
    Task<PagedResult<StaffDailyAttendanceSummaryDto>> GetPagedAsync(int pageNumber, int pageSize, CancellationToken ct = default);

    /// <summary>
    /// Tenant-wide filtered search. The narrow reads above each answer one question; this
    /// composes them, which is what a filterable grid needs.
    /// </summary>
    Task<PagedResult<StaffDailyAttendanceSummaryDto>> SearchAsync(
        StaffDailyAttendanceSearchDto filter, int pageNumber, int pageSize, CancellationToken ct = default);

    Task<StaffDailyAttendanceDto> CreateAsync(CreateStaffDailyAttendanceDto dto, Guid tenantId, Guid userId, CancellationToken ct = default);
    Task<StaffDailyAttendanceDto> UpdateAsync(UpdateStaffDailyAttendanceDto dto, Guid userId, CancellationToken ct = default);
    Task<StaffDailyAttendanceDto> VerifyAsync(VerifyAttendanceDto dto, Guid userId, CancellationToken ct = default);
    Task<StaffDailyAttendanceDto> ApproveExceptionAsync(Guid attendanceId, string? notes, Guid userId, CancellationToken ct = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken ct = default);
}

#endregion

// ============================================================================
// STAFF ATTENDANCE LOG SERVICE
// ============================================================================

#region Staff Attendance Log Service

public interface IStaffAttendanceLogService
{
    Task<StaffAttendanceLogDto> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IEnumerable<StaffAttendanceLogSummaryDto>> GetByEmployeeIdAsync(Guid employeeId, DateTime from, DateTime to, CancellationToken ct = default);
    Task<IEnumerable<StaffAttendanceLogSummaryDto>> GetUnprocessedLogsAsync(CancellationToken ct = default);
    Task<IEnumerable<StaffAttendanceLogSummaryDto>> GetByDeviceIdAsync(Guid deviceId, DateTime from, DateTime to, CancellationToken ct = default);
    Task<PagedResult<StaffAttendanceLogSummaryDto>> GetPagedAsync(int pageNumber, int pageSize, CancellationToken ct = default);

    Task<StaffAttendanceLogDto> CreateAsync(CreateStaffAttendanceLogDto dto, Guid tenantId, Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Self-service punch for the authenticated employee. Optional GPS triggers geofence verification.
    /// Other capture channels (bulk import, manual records) are unaffected.
    /// </summary>
    Task<StaffAttendancePunchResultDto> PunchAsync(
        StaffAttendancePunchDto dto,
        Guid tenantId,
        Guid employeeId,
        Guid userId,
        CancellationToken ct = default);

    /// <summary>
    /// Processes a single unprocessed log, converting it into a DailyAttendance record.
    /// Returns the attendance record id created or updated.
    /// </summary>
    Task<Guid?> ProcessLogAsync(Guid logId, Guid userId, CancellationToken ct = default);

    Task<bool> DeleteAsync(Guid id, CancellationToken ct = default);
}

#endregion

// ============================================================================
// STAFF ATTENDANCE REGULARIZATION SERVICE
// ============================================================================

#region Staff Attendance Regularization Service

public interface IStaffAttendanceRegularizationService
{
    Task<StaffAttendanceRegularizationDto> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<StaffAttendanceRegularizationDto?> GetByRegularizationNumberAsync(string regularizationNumber, CancellationToken ct = default);
    Task<IEnumerable<StaffAttendanceRegularizationSummaryDto>> GetByEmployeeIdAsync(Guid employeeId, CancellationToken ct = default);
    Task<IEnumerable<StaffAttendanceRegularizationSummaryDto>> GetByAttendanceIdAsync(Guid attendanceId, CancellationToken ct = default);
    Task<IEnumerable<StaffAttendanceRegularizationSummaryDto>> GetByStatusAsync(AttendanceRegularizationStatus status, CancellationToken ct = default);
    Task<IEnumerable<StaffAttendanceRegularizationSummaryDto>> GetPendingApprovalAsync(CancellationToken ct = default);
    Task<PagedResult<StaffAttendanceRegularizationSummaryDto>> GetPagedAsync(int pageNumber, int pageSize, CancellationToken ct = default);

    Task<StaffAttendanceRegularizationDto> CreateAsync(CreateStaffAttendanceRegularizationDto dto, Guid tenantId, Guid userId, CancellationToken ct = default);
    Task<StaffAttendanceRegularizationDto> UpdateAsync(UpdateStaffAttendanceRegularizationDto dto, Guid userId, CancellationToken ct = default);
    Task<StaffAttendanceRegularizationDto> ApproveAsync(ApproveRegularizationDto dto, Guid userId, CancellationToken ct = default);
    Task<StaffAttendanceRegularizationDto> RejectAsync(RejectRegularizationDto dto, Guid userId, CancellationToken ct = default);

    /// <summary>Applies an approved regularization to its linked DailyAttendance record.</summary>
    Task<bool> ApplyAsync(Guid regularizationId, Guid userId, CancellationToken ct = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken ct = default);
}

#endregion

// ============================================================================
// STAFF MONTHLY ATTENDANCE SUMMARY SERVICE
// ============================================================================

#region Staff Monthly Attendance Summary Service

public interface IStaffMonthlyAttendanceSummaryService
{
    Task<StaffMonthlyAttendanceSummaryDto> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<StaffMonthlyAttendanceSummaryDto?> GetByEmployeeAndPeriodAsync(Guid employeeId, int year, int month, CancellationToken ct = default);
    Task<IEnumerable<StaffMonthlyAttendanceSummaryDto>> GetByEmployeeAndYearAsync(Guid employeeId, int year, CancellationToken ct = default);
    Task<IEnumerable<StaffMonthlyAttendanceSummaryDto>> GetByYearAndMonthAsync(int year, int month, CancellationToken ct = default);
    Task<IEnumerable<StaffMonthlyAttendanceSummaryDto>> GetByPayPeriodIdAsync(Guid payPeriodId, CancellationToken ct = default);
    Task<IEnumerable<StaffMonthlyAttendanceSummaryDto>> GetUnfinalizedAsync(int year, int month, CancellationToken ct = default);

    /// <summary>Computes and persists the monthly summary for an employee and period, replacing any existing draft.</summary>
    Task<StaffMonthlyAttendanceSummaryDto> RecalculateAsync(Guid employeeId, int year, int month, Guid userId, CancellationToken ct = default);
    Task<StaffMonthlyAttendanceSummaryDto> FinalizeAsync(Guid summaryId, Guid userId, CancellationToken ct = default);
    Task<StaffMonthlyAttendanceSummaryDto> UpdateAsync(UpdateStaffMonthlyAttendanceSummaryDto dto, Guid userId, CancellationToken ct = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken ct = default);
}

#endregion

// ============================================================================
// STAFF BULK ATTENDANCE IMPORT SERVICE
// ============================================================================

#region Staff Bulk Attendance Import Service

public interface IStaffBulkAttendanceImportService
{
    Task<StaffBulkAttendanceImportDto> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<StaffBulkAttendanceImportDto?> GetByImportReferenceAsync(string importReference, CancellationToken ct = default);
    Task<IEnumerable<StaffBulkAttendanceImportSummaryDto>> GetByStatusAsync(AttendanceImportStatus status, CancellationToken ct = default);
    Task<PagedResult<StaffBulkAttendanceImportSummaryDto>> GetPagedAsync(int pageNumber, int pageSize, CancellationToken ct = default);
    Task<IEnumerable<StaffBulkAttendanceImportRowDto>> GetRowsAsync(Guid importId, CancellationToken ct = default);
    Task<IEnumerable<StaffBulkAttendanceImportRowDto>> GetFailedRowsAsync(Guid importId, CancellationToken ct = default);

    /// <summary>Creates the import batch record and validates the uploaded payload before processing.</summary>
    Task<StaffBulkAttendanceImportDto> InitiateAsync(CreateStaffBulkAttendanceImportDto dto, Guid tenantId, Guid userId, CancellationToken ct = default);

    /// <summary>Processes all pending rows in an import batch, updating success/failure counts.</summary>
    Task<StaffBulkAttendanceImportDto> ProcessAsync(Guid importId, Guid userId, CancellationToken ct = default);

    Task<bool> DeleteAsync(Guid id, CancellationToken ct = default);
}

#endregion
