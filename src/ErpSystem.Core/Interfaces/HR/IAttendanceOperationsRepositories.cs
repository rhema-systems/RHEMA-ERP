using ErpSystem.Core.Entities.HR.StaffAttendance;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Interfaces.HR;

// ============================================================================
// GEOFENCE ZONE
// ============================================================================

#region Geofence Zone

public interface IGeofenceZoneRepository : IGenericRepository<GeofenceZone>
{
    /// <summary>Returns all active geofence zones.</summary>
    Task<IEnumerable<GeofenceZone>> GetActiveZonesAsync();

    /// <summary>Returns all geofence zones associated with a specific location.</summary>
    Task<IEnumerable<GeofenceZone>> GetByLocationIdAsync(Guid locationId);

    /// <summary>Returns zones filtered by enforcement mode — soft or hard.</summary>
    Task<IEnumerable<GeofenceZone>> GetByEnforcementModeAsync(bool hardEnforcement);
}

#endregion

// ============================================================================
// ATTENDANCE LOCATION VERIFICATION LOG
// ============================================================================

#region Attendance Location Verification Log

public interface IAttendanceLocationVerificationLogRepository : IGenericRepository<AttendanceLocationVerificationLog>
{
    /// <summary>Returns all verification logs for a specific attendance punch log.</summary>
    Task<IEnumerable<AttendanceLocationVerificationLog>> GetByAttendanceLogIdAsync(Guid attendanceLogId);

    /// <summary>Returns all verification logs for an employee within a date/time range.</summary>
    Task<IEnumerable<AttendanceLocationVerificationLog>> GetByEmployeeIdAsync(Guid employeeId, DateTime from, DateTime to);

    /// <summary>Returns verification attempts that resulted in a failed or outside-zone status.</summary>
    Task<IEnumerable<AttendanceLocationVerificationLog>> GetFailedVerificationsAsync(DateTime from, DateTime to);

    /// <summary>Returns all verification logs checked against a specific geofence zone.</summary>
    Task<IEnumerable<AttendanceLocationVerificationLog>> GetByGeofenceZoneIdAsync(Guid geofenceZoneId, DateTime from, DateTime to);
}

#endregion

// ============================================================================
// REMOTE WORK REQUEST
// ============================================================================

#region Remote Work Request

public interface IRemoteWorkRequestRepository : IGenericRepository<RemoteWorkRequest>
{
    /// <summary>Returns the remote work request matching the unique request number, or null.</summary>
    Task<RemoteWorkRequest?> GetByRequestNumberAsync(string requestNumber);

    /// <summary>Returns all remote work requests submitted by an employee, newest first.</summary>
    Task<IEnumerable<RemoteWorkRequest>> GetByEmployeeIdAsync(Guid employeeId);

    /// <summary>Returns remote work requests filtered by status.</summary>
    Task<IEnumerable<RemoteWorkRequest>> GetByStatusAsync(RemoteWorkRequestStatus status);

    /// <summary>Returns all requests with Pending status awaiting manager approval.</summary>
    Task<IEnumerable<RemoteWorkRequest>> GetPendingApprovalAsync();

    /// <summary>
    /// Returns the approved remote work request that covers the given date for an employee, or null.
    /// Used to validate whether IsRemoteWork = true is permissible for a DailyAttendance record.
    /// </summary>
    Task<RemoteWorkRequest?> GetApprovedRequestCoveringDateAsync(Guid employeeId, DateOnly date);

    /// <summary>Returns requests whose date range overlaps with the specified period.</summary>
    Task<IEnumerable<RemoteWorkRequest>> GetByDateRangeAsync(DateOnly from, DateOnly to);
}

#endregion

// ============================================================================
// HOLIDAY CALENDAR
// ============================================================================

#region Holiday Calendar

public interface IHolidayCalendarRepository : IGenericRepository<HolidayCalendar>
{
    /// <summary>Returns the tenant-wide default holiday calendar, or null if none is set.</summary>
    Task<HolidayCalendar?> GetDefaultCalendarAsync();

    /// <summary>Returns all active holiday calendars.</summary>
    Task<IEnumerable<HolidayCalendar>> GetActiveCalendarsAsync();

    /// <summary>Returns a holiday calendar fully loaded with its public holidays for a specific year.</summary>
    Task<HolidayCalendar?> GetWithHolidaysAsync(Guid id, int? year = null);
}

#endregion

// ============================================================================
// PUBLIC HOLIDAY
// ============================================================================

#region Public Holiday

public interface IPublicHolidayRepository : IGenericRepository<PublicHoliday>
{
    /// <summary>Returns all public holidays in a calendar, ordered by date.</summary>
    Task<IEnumerable<PublicHoliday>> GetByCalendarIdAsync(Guid calendarId);

    /// <summary>Returns all holidays in a calendar for a specific year.</summary>
    Task<IEnumerable<PublicHoliday>> GetByCalendarAndYearAsync(Guid calendarId, int year);

    /// <summary>
    /// Returns the public holiday whose date range covers the specified date in the given calendar, or null.
    /// Used when processing attendance to detect whether a day is a public holiday.
    /// </summary>
    Task<PublicHoliday?> GetHolidayCoveringDateAsync(Guid calendarId, DateOnly date);

    /// <summary>Returns all recurring holidays in a calendar regardless of year.</summary>
    Task<IEnumerable<PublicHoliday>> GetRecurringHolidaysAsync(Guid calendarId);

    /// <summary>Returns all holidays whose date range overlaps the specified period (tenant-wide).</summary>
    Task<IEnumerable<PublicHoliday>> GetHolidaysInRangeAsync(DateOnly from, DateOnly to);

    /// <summary>Returns all holidays in the specified calendar year (tenant-wide).</summary>
    Task<IEnumerable<PublicHoliday>> GetByYearAsync(int year);
}

#endregion

// ============================================================================
// PAY PERIOD
// ============================================================================

#region Pay Period

public interface IPayPeriodRepository : IGenericRepository<PayPeriod>
{
    /// <summary>Returns pay periods filtered by status.</summary>
    Task<IEnumerable<PayPeriod>> GetByStatusAsync(PayPeriodStatus status);

    /// <summary>Returns the currently open pay period (Status = Open), or null if none is active.</summary>
    Task<PayPeriod?> GetCurrentOpenPeriodAsync();

    /// <summary>Returns the pay period whose date range covers the specified date, or null.</summary>
    Task<PayPeriod?> GetPeriodCoveringDateAsync(DateOnly date);

    /// <summary>Returns pay periods filtered by type (Weekly, BiWeekly, Monthly, etc.).</summary>
    Task<IEnumerable<PayPeriod>> GetByTypeAsync(PayPeriodType type);

    /// <summary>Returns a pay period fully loaded with its attendance summaries and payroll export history.</summary>
    Task<PayPeriod?> GetWithSummariesAsync(Guid id);
}

#endregion

// ============================================================================
// STAFF ATTENDANCE PAYROLL EXPORT
// ============================================================================

#region Staff Attendance Payroll Export

public interface IStaffAttendancePayrollExportRepository : IGenericRepository<StaffAttendancePayrollExport>
{
    /// <summary>Returns the export batch matching the unique export reference, or null.</summary>
    Task<StaffAttendancePayrollExport?> GetByExportReferenceAsync(string exportReference);

    /// <summary>Returns all payroll export batches for a specific pay period.</summary>
    Task<IEnumerable<StaffAttendancePayrollExport>> GetByPayPeriodIdAsync(Guid payPeriodId);

    /// <summary>Returns export batches filtered by status.</summary>
    Task<IEnumerable<StaffAttendancePayrollExport>> GetByStatusAsync(PayrollExportStatus status);

    /// <summary>Returns the most recent successful export for a pay period, or null.</summary>
    Task<StaffAttendancePayrollExport?> GetLatestSuccessfulExportForPeriodAsync(Guid payPeriodId);
}

#endregion
