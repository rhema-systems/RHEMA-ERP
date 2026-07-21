using ErpSystem.Core.Entities.HR.StaffAttendance;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Interfaces.HR;

// ============================================================================
// STAFF ATTENDANCE RECORD
// ============================================================================

#region Staff Attendance Record

public interface IStaffAttendanceRecordRepository : IGenericRepository<StaffAttendanceRecord>
{
    /// <summary>Returns the single record for an employee on a specific date, or null if none exists.</summary>
    Task<StaffAttendanceRecord?> GetByEmployeeAndDateAsync(Guid employeeId, DateOnly date);

    /// <summary>Returns all records for an employee within a date range, ordered by date ascending.</summary>
    Task<IEnumerable<StaffAttendanceRecord>> GetByEmployeeIdAsync(Guid employeeId, DateOnly from, DateOnly to);

    /// <summary>Returns all records for a specific date across all employees, with employee details loaded.</summary>
    Task<IEnumerable<StaffAttendanceRecord>> GetByDateAsync(DateOnly date);

    /// <summary>Returns all records within a date range, with employee details loaded.</summary>
    Task<IEnumerable<StaffAttendanceRecord>> GetByDateRangeAsync(DateOnly from, DateOnly to);

    /// <summary>Returns records matching the given status within a date range.</summary>
    Task<IEnumerable<StaffAttendanceRecord>> GetByStatusAsync(StaffAttendanceStatus status, DateOnly from, DateOnly to);
}

#endregion

// ============================================================================
// STAFF DAILY ATTENDANCE
// ============================================================================

#region Staff Daily Attendance

public interface IStaffDailyAttendanceRepository : IGenericRepository<StaffDailyAttendance>
{
    /// <summary>Returns the full-detail daily attendance record for an employee on a specific date, or null.</summary>
    Task<StaffDailyAttendance?> GetByEmployeeAndDateAsync(Guid employeeId, DateOnly date);

    /// <summary>Returns all daily attendance records for an employee within a date range.</summary>
    Task<IEnumerable<StaffDailyAttendance>> GetByEmployeeIdAsync(Guid employeeId, DateOnly from, DateOnly to);

    /// <summary>Returns all records across all employees for a specific date, with employee details loaded.</summary>
    Task<IEnumerable<StaffDailyAttendance>> GetByDateAsync(DateOnly date);

    /// <summary>Returns records filtered by status within a date range.</summary>
    Task<IEnumerable<StaffDailyAttendance>> GetByStatusAsync(StaffAttendanceStatus status, DateOnly from, DateOnly to);

    /// <summary>Returns records for an employee filtered by status within a date range.</summary>
    Task<IEnumerable<StaffDailyAttendance>> GetByEmployeeAndStatusAsync(Guid employeeId, StaffAttendanceStatus status, DateOnly from, DateOnly to);

    /// <summary>Returns records that require verification but have not yet been verified.</summary>
    Task<IEnumerable<StaffDailyAttendance>> GetPendingVerificationAsync();

    /// <summary>Returns records that have an unresolved exception (HasException = true and ExceptionApproved = false).</summary>
    Task<IEnumerable<StaffDailyAttendance>> GetWithOpenExceptionsAsync();

    /// <summary>Returns records where overtime was worked within a date range, optionally scoped to an employee.</summary>
    Task<IEnumerable<StaffDailyAttendance>> GetWithOvertimeAsync(DateOnly from, DateOnly to, Guid? employeeId = null);

    /// <summary>Returns late attendance records within a date range, optionally scoped to one employee.</summary>
    Task<IEnumerable<StaffDailyAttendance>> GetLateAttendancesAsync(DateOnly from, DateOnly to, Guid? employeeId = null);

    /// <summary>Returns remote-work days within a date range, optionally scoped to one employee.</summary>
    Task<IEnumerable<StaffDailyAttendance>> GetRemoteWorkDaysAsync(DateOnly from, DateOnly to, Guid? employeeId = null);

    /// <summary>Returns all records assigned to a specific pay period.</summary>
    Task<IEnumerable<StaffDailyAttendance>> GetByPayPeriodIdAsync(Guid payPeriodId);

    /// <summary>Returns all records checked in at a specific location within a date range.</summary>
    Task<IEnumerable<StaffDailyAttendance>> GetByLocationIdAsync(Guid locationId, DateOnly from, DateOnly to);

    /// <summary>Returns a fully-loaded daily attendance record including logs, regularizations, and all navigations.</summary>
    Task<StaffDailyAttendance?> GetWithFullDetailsAsync(Guid id);
}

#endregion

// ============================================================================
// STAFF ATTENDANCE LOG
// ============================================================================

#region Staff Attendance Log

public interface IStaffAttendanceLogRepository : IGenericRepository<StaffAttendanceLog>
{
    /// <summary>Returns all punch logs for an employee within a date/time range, ordered by log time.</summary>
    Task<IEnumerable<StaffAttendanceLog>> GetByEmployeeIdAsync(Guid employeeId, DateTime from, DateTime to);

    /// <summary>Returns all punch logs that have not yet been processed into a DailyAttendance record.</summary>
    Task<IEnumerable<StaffAttendanceLog>> GetUnprocessedLogsAsync();

    /// <summary>Returns unprocessed punch logs for a specific employee, oldest first.</summary>
    Task<IEnumerable<StaffAttendanceLog>> GetUnprocessedLogsByEmployeeAsync(Guid employeeId);

    /// <summary>Returns all logs originating from a specific device within a date/time range.</summary>
    Task<IEnumerable<StaffAttendanceLog>> GetByDeviceIdAsync(Guid deviceId, DateTime from, DateTime to);

    /// <summary>Returns all logs linked to a specific DailyAttendance record.</summary>
    Task<IEnumerable<StaffAttendanceLog>> GetLinkedToAttendanceAsync(Guid attendanceId);

    /// <summary>Returns all logs across all employees within a date/time range.</summary>
    Task<IEnumerable<StaffAttendanceLog>> GetByDateRangeAsync(DateTime from, DateTime to);
}

#endregion

// ============================================================================
// STAFF ATTENDANCE REGULARIZATION
// ============================================================================

#region Staff Attendance Regularization

public interface IStaffAttendanceRegularizationRepository : IGenericRepository<StaffAttendanceRegularization>
{
    /// <summary>Returns the regularization matching the unique regularization number.</summary>
    Task<StaffAttendanceRegularization?> GetByRegularizationNumberAsync(string regularizationNumber);

    /// <summary>Returns all regularization requests submitted by an employee, newest first.</summary>
    Task<IEnumerable<StaffAttendanceRegularization>> GetByEmployeeIdAsync(Guid employeeId);

    /// <summary>Returns all regularization requests for a specific daily attendance record.</summary>
    Task<IEnumerable<StaffAttendanceRegularization>> GetByAttendanceIdAsync(Guid attendanceId);

    /// <summary>Returns regularization requests filtered by status.</summary>
    Task<IEnumerable<StaffAttendanceRegularization>> GetByStatusAsync(AttendanceRegularizationStatus status);

    /// <summary>Returns all requests with Pending status that are awaiting manager approval.</summary>
    Task<IEnumerable<StaffAttendanceRegularization>> GetPendingApprovalAsync();

    /// <summary>Returns requests that have been approved and applied to the attendance record.</summary>
    Task<IEnumerable<StaffAttendanceRegularization>> GetAppliedAsync(DateOnly from, DateOnly to);

    /// <summary>Returns a fully-loaded regularization including attendance, employee, and approver details.</summary>
    Task<StaffAttendanceRegularization?> GetWithFullDetailsAsync(Guid id);
}

#endregion

// ============================================================================
// STAFF MONTHLY ATTENDANCE SUMMARY
// ============================================================================

#region Staff Monthly Attendance Summary

public interface IStaffMonthlyAttendanceSummaryRepository : IGenericRepository<StaffMonthlyAttendanceSummary>
{
    /// <summary>Returns the summary for a specific employee for the given year and month, or null.</summary>
    Task<StaffMonthlyAttendanceSummary?> GetByEmployeeAndPeriodAsync(Guid employeeId, int year, int month);

    /// <summary>Returns all monthly summaries for an employee across all months in a year.</summary>
    Task<IEnumerable<StaffMonthlyAttendanceSummary>> GetByEmployeeAndYearAsync(Guid employeeId, int year);

    /// <summary>Returns all summaries for a given year and month across all employees.</summary>
    Task<IEnumerable<StaffMonthlyAttendanceSummary>> GetByYearAndMonthAsync(int year, int month);

    /// <summary>Returns summaries linked to a specific pay period.</summary>
    Task<IEnumerable<StaffMonthlyAttendanceSummary>> GetByPayPeriodIdAsync(Guid payPeriodId);

    /// <summary>Returns summaries that have not yet been finalized (IsFinalized = false).</summary>
    Task<IEnumerable<StaffMonthlyAttendanceSummary>> GetUnfinalizedAsync(int year, int month);
}

#endregion

// ============================================================================
// STAFF BULK ATTENDANCE IMPORT
// ============================================================================

#region Staff Bulk Attendance Import

public interface IStaffBulkAttendanceImportRepository : IGenericRepository<StaffBulkAttendanceImport>
{
    /// <summary>Returns the import batch matching the unique import reference, or null.</summary>
    Task<StaffBulkAttendanceImport?> GetByImportReferenceAsync(string importReference);

    /// <summary>Returns import batches filtered by status, newest first.</summary>
    Task<IEnumerable<StaffBulkAttendanceImport>> GetByStatusAsync(AttendanceImportStatus status);

    /// <summary>Returns import batches initiated by a specific employee, newest first.</summary>
    Task<IEnumerable<StaffBulkAttendanceImport>> GetByImportedByAsync(Guid employeeId);

    /// <summary>Returns a fully-loaded import batch including all row results.</summary>
    Task<StaffBulkAttendanceImport?> GetWithRowsAsync(Guid id);
}

#endregion

// ============================================================================
// STAFF BULK ATTENDANCE IMPORT ROW
// ============================================================================

#region Staff Bulk Attendance Import Row

public interface IStaffBulkAttendanceImportRowRepository : IGenericRepository<StaffBulkAttendanceImportRow>
{
    /// <summary>Returns all rows belonging to an import batch, ordered by row number.</summary>
    Task<IEnumerable<StaffBulkAttendanceImportRow>> GetByImportIdAsync(Guid importId);

    /// <summary>Returns only the failed rows for an import batch.</summary>
    Task<IEnumerable<StaffBulkAttendanceImportRow>> GetFailedRowsAsync(Guid importId);

    /// <summary>Returns all import rows associated with a resolved employee across all batches.</summary>
    Task<IEnumerable<StaffBulkAttendanceImportRow>> GetByEmployeeIdAsync(Guid employeeId);
}

#endregion
