using ErpSystem.Core.Entities.HR.StaffAttendance;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Data.Repositories.HR;

// ============================================================================
// GEOFENCE ZONE REPOSITORY
// ============================================================================

#region Geofence Zone Repository

public class GeofenceZoneRepository : GenericRepository<GeofenceZone>, IGeofenceZoneRepository
{
    public GeofenceZoneRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<GeofenceZone>> GetActiveZonesAsync()
    {
        return await _dbSet
            .Where(z => z.IsActive && !z.IsDeleted)
            .OrderBy(z => z.ZoneName)
            .ToListAsync();
    }

    public async Task<IEnumerable<GeofenceZone>> GetByLocationIdAsync(Guid locationId)
    {
        return await _dbSet
            .Include(z => z.Locations)
            .Where(z => z.Locations.Any(l => l.Id == locationId) && z.IsActive && !z.IsDeleted)
            .OrderBy(z => z.ZoneName)
            .ToListAsync();
    }

    public async Task<IEnumerable<GeofenceZone>> GetByEnforcementModeAsync(bool hardEnforcement)
    {
        return await _dbSet
            .Where(z => z.HardEnforcement == hardEnforcement && z.IsActive && !z.IsDeleted)
            .OrderBy(z => z.ZoneName)
            .ToListAsync();
    }
}

#endregion

// ============================================================================
// ATTENDANCE LOCATION VERIFICATION LOG REPOSITORY
// ============================================================================

#region Attendance Location Verification Log Repository

public class AttendanceLocationVerificationLogRepository : GenericRepository<AttendanceLocationVerificationLog>, IAttendanceLocationVerificationLogRepository
{
    public AttendanceLocationVerificationLogRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<AttendanceLocationVerificationLog>> GetByAttendanceLogIdAsync(Guid attendanceLogId)
    {
        return await _dbSet
            .Include(v => v.GeofenceZone)
            .Where(v => v.AttendanceLogId == attendanceLogId && !v.IsDeleted)
            .OrderBy(v => v.VerificationDateTime)
            .ToListAsync();
    }

    public async Task<IEnumerable<AttendanceLocationVerificationLog>> GetByEmployeeIdAsync(Guid employeeId, DateTime from, DateTime to)
    {
        return await _dbSet
            .Include(v => v.AttendanceLog)
            .Include(v => v.GeofenceZone)
            .Where(v => v.EmployeeId == employeeId
                     && v.VerificationDateTime >= from
                     && v.VerificationDateTime <= to
                     && !v.IsDeleted)
            .OrderBy(v => v.VerificationDateTime)
            .ToListAsync();
    }

    public async Task<IEnumerable<AttendanceLocationVerificationLog>> GetFailedVerificationsAsync(DateTime from, DateTime to)
    {
        return await _dbSet
            .Include(v => v.Employee)
            .Include(v => v.GeofenceZone)
            .Where(v => v.Status == LocationVerificationStatus.OutsideZone
                     && v.VerificationDateTime >= from
                     && v.VerificationDateTime <= to
                     && !v.IsDeleted)
            .OrderBy(v => v.VerificationDateTime)
            .ToListAsync();
    }

    public async Task<IEnumerable<AttendanceLocationVerificationLog>> GetByGeofenceZoneIdAsync(Guid geofenceZoneId, DateTime from, DateTime to)
    {
        return await _dbSet
            .Include(v => v.Employee)
            .Include(v => v.AttendanceLog)
            .Where(v => v.GeofenceZoneId == geofenceZoneId
                     && v.VerificationDateTime >= from
                     && v.VerificationDateTime <= to
                     && !v.IsDeleted)
            .OrderBy(v => v.VerificationDateTime)
            .ToListAsync();
    }
}

#endregion

// ============================================================================
// REMOTE WORK REQUEST REPOSITORY
// ============================================================================

#region Remote Work Request Repository

public class RemoteWorkRequestRepository : GenericRepository<RemoteWorkRequest>, IRemoteWorkRequestRepository
{
    public RemoteWorkRequestRepository(ApplicationDbContext context) : base(context) { }

    public async Task<RemoteWorkRequest?> GetByRequestNumberAsync(string requestNumber)
    {
        return await _dbSet
            .Include(r => r.Employee)
            .Include(r => r.ApprovedBy)
            .FirstOrDefaultAsync(r => r.RequestNumber == requestNumber && !r.IsDeleted);
    }

    public async Task<IEnumerable<RemoteWorkRequest>> GetByEmployeeIdAsync(Guid employeeId)
    {
        return await _dbSet
            .Include(r => r.ApprovedBy)
            .Where(r => r.EmployeeId == employeeId && !r.IsDeleted)
            .OrderByDescending(r => r.StartDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<RemoteWorkRequest>> GetByStatusAsync(RemoteWorkRequestStatus status)
    {
        return await _dbSet
            .Include(r => r.Employee)
            .Include(r => r.ApprovedBy)
            .Where(r => r.Status == status && !r.IsDeleted)
            .OrderBy(r => r.StartDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<RemoteWorkRequest>> GetPendingApprovalAsync()
    {
        return await _dbSet
            .Include(r => r.Employee)
            .Where(r => r.Status == RemoteWorkRequestStatus.Pending && !r.IsDeleted)
            .OrderBy(r => r.StartDate)
            .ToListAsync();
    }

    public async Task<RemoteWorkRequest?> GetApprovedRequestCoveringDateAsync(Guid employeeId, DateOnly date)
    {
        return await _dbSet
            .Include(r => r.Employee)
            .FirstOrDefaultAsync(r => r.EmployeeId == employeeId
                                   && r.Status == RemoteWorkRequestStatus.Approved
                                   && r.StartDate <= date
                                   && r.EndDate >= date
                                   && !r.IsDeleted);
    }

    public async Task<IEnumerable<RemoteWorkRequest>> GetByDateRangeAsync(DateOnly from, DateOnly to)
    {
        return await _dbSet
            .Include(r => r.Employee)
            .Where(r => r.StartDate <= to && r.EndDate >= from && !r.IsDeleted)
            .OrderBy(r => r.StartDate)
            .ThenBy(r => r.Employee.LastName)
            .ToListAsync();
    }
}

#endregion

// ============================================================================
// HOLIDAY CALENDAR REPOSITORY
// ============================================================================

#region Holiday Calendar Repository

public class HolidayCalendarRepository : GenericRepository<HolidayCalendar>, IHolidayCalendarRepository
{
    public HolidayCalendarRepository(ApplicationDbContext context) : base(context) { }

    public async Task<HolidayCalendar?> GetDefaultCalendarAsync()
    {
        return await _dbSet
            .Include(c => c.PublicHolidays)
            .FirstOrDefaultAsync(c => c.IsDefault && c.IsActive && !c.IsDeleted);
    }

    public async Task<IEnumerable<HolidayCalendar>> GetActiveCalendarsAsync()
    {
        return await _dbSet
            .Where(c => c.IsActive && !c.IsDeleted)
            .OrderBy(c => c.CalendarName)
            .ToListAsync();
    }

    public async Task<HolidayCalendar?> GetWithHolidaysAsync(Guid id, int? year = null)
    {
        var query = _dbSet.Where(c => c.Id == id && !c.IsDeleted);

        if (year.HasValue)
        {
            return await query
                .Include(c => c.PublicHolidays.Where(h => h.DateFrom.Year == year.Value && !h.IsDeleted))
                .FirstOrDefaultAsync();
        }

        return await query
            .Include(c => c.PublicHolidays.Where(h => !h.IsDeleted))
            .FirstOrDefaultAsync();
    }
}

#endregion

// ============================================================================
// PUBLIC HOLIDAY REPOSITORY
// ============================================================================

#region Public Holiday Repository

public class PublicHolidayRepository : GenericRepository<PublicHoliday>, IPublicHolidayRepository
{
    public PublicHolidayRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<PublicHoliday>> GetByCalendarIdAsync(Guid calendarId)
    {
        return await _dbSet
            .Include(h => h.HolidayCalendar)
            .Where(h => h.HolidayCalendarId == calendarId && h.IsActive && !h.IsDeleted)
            .OrderBy(h => h.DateFrom)
            .ToListAsync();
    }

    public async Task<IEnumerable<PublicHoliday>> GetByCalendarAndYearAsync(Guid calendarId, int year)
    {
        return await _dbSet
            .Where(h => h.HolidayCalendarId == calendarId
                     && h.DateFrom.Year == year
                     && h.IsActive
                     && !h.IsDeleted)
            .OrderBy(h => h.DateFrom)
            .ToListAsync();
    }

    public async Task<PublicHoliday?> GetHolidayCoveringDateAsync(Guid calendarId, DateOnly date)
    {
        return await _dbSet
            .FirstOrDefaultAsync(h => h.HolidayCalendarId == calendarId
                                   && h.DateFrom <= date
                                   && h.DateTo >= date
                                   && h.IsActive
                                   && !h.IsDeleted);
    }

    public async Task<IEnumerable<PublicHoliday>> GetRecurringHolidaysAsync(Guid calendarId)
    {
        return await _dbSet
            .Where(h => h.HolidayCalendarId == calendarId && h.IsRecurringAnnually && h.IsActive && !h.IsDeleted)
            .OrderBy(h => h.DateFrom.Month)
            .ThenBy(h => h.DateFrom.Day)
            .ToListAsync();
    }

    public async Task<IEnumerable<PublicHoliday>> GetHolidaysInRangeAsync(DateOnly from, DateOnly to)
    {
        return await _dbSet
            .Where(h => h.DateFrom <= to && h.DateTo >= from && h.IsActive && !h.IsDeleted)
            .OrderBy(h => h.DateFrom)
            .ToListAsync();
    }

    public async Task<IEnumerable<PublicHoliday>> GetByYearAsync(int year)
    {
        return await _dbSet
            .Where(h => (h.DateFrom.Year == year || h.DateTo.Year == year) && h.IsActive && !h.IsDeleted)
            .OrderBy(h => h.DateFrom)
            .ToListAsync();
    }
}

#endregion

// ============================================================================
// PAY PERIOD REPOSITORY
// ============================================================================

#region Pay Period Repository

public class PayPeriodRepository : GenericRepository<PayPeriod>, IPayPeriodRepository
{
    public PayPeriodRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<PayPeriod>> GetByStatusAsync(PayPeriodStatus status)
    {
        return await _dbSet
            .Where(p => p.Status == status && !p.IsDeleted)
            .OrderByDescending(p => p.StartDate)
            .ToListAsync();
    }

    public async Task<PayPeriod?> GetCurrentOpenPeriodAsync()
    {
        return await _dbSet
            .FirstOrDefaultAsync(p => p.Status == PayPeriodStatus.Open && !p.IsDeleted);
    }

    public async Task<PayPeriod?> GetPeriodCoveringDateAsync(DateOnly date)
    {
        return await _dbSet
            .FirstOrDefaultAsync(p => p.StartDate <= date && p.EndDate >= date && !p.IsDeleted);
    }

    public async Task<IEnumerable<PayPeriod>> GetByTypeAsync(PayPeriodType type)
    {
        return await _dbSet
            .Where(p => p.Type == type && !p.IsDeleted)
            .OrderByDescending(p => p.StartDate)
            .ToListAsync();
    }

    public async Task<PayPeriod?> GetWithSummariesAsync(Guid id)
    {
        return await _dbSet
            .Include(p => p.ClosedBy)
            .Include(p => p.ExportedBy)
            .Include(p => p.AttendanceSummaries).ThenInclude(s => s.Employee)
            .Include(p => p.PayrollExports)
            .FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted);
    }
}

#endregion

// ============================================================================
// STAFF ATTENDANCE PAYROLL EXPORT REPOSITORY
// ============================================================================

#region Staff Attendance Payroll Export Repository

public class StaffAttendancePayrollExportRepository : GenericRepository<StaffAttendancePayrollExport>, IStaffAttendancePayrollExportRepository
{
    public StaffAttendancePayrollExportRepository(ApplicationDbContext context) : base(context) { }

    public async Task<StaffAttendancePayrollExport?> GetByExportReferenceAsync(string exportReference)
    {
        return await _dbSet
            .Include(e => e.ExportedBy)
            .Include(e => e.PayPeriod)
            .FirstOrDefaultAsync(e => e.ExportReference == exportReference && !e.IsDeleted);
    }

    public async Task<IEnumerable<StaffAttendancePayrollExport>> GetByPayPeriodIdAsync(Guid payPeriodId)
    {
        return await _dbSet
            .Include(e => e.ExportedBy)
            .Where(e => e.PayPeriodId == payPeriodId && !e.IsDeleted)
            .OrderByDescending(e => e.ExportDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffAttendancePayrollExport>> GetByStatusAsync(PayrollExportStatus status)
    {
        return await _dbSet
            .Include(e => e.ExportedBy)
            .Include(e => e.PayPeriod)
            .Where(e => e.Status == status && !e.IsDeleted)
            .OrderByDescending(e => e.ExportDate)
            .ToListAsync();
    }

    public async Task<StaffAttendancePayrollExport?> GetLatestSuccessfulExportForPeriodAsync(Guid payPeriodId)
    {
        return await _dbSet
            .Include(e => e.ExportedBy)
            .Where(e => e.PayPeriodId == payPeriodId
                     && e.Status == PayrollExportStatus.Completed
                     && !e.IsDeleted)
            .OrderByDescending(e => e.ExportDate)
            .FirstOrDefaultAsync();
    }
}

#endregion
