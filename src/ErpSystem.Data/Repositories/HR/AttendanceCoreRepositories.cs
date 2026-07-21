using ErpSystem.Core.Entities.HR.StaffAttendance;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Data.Repositories.HR;

// ============================================================================
// STAFF ATTENDANCE RECORD REPOSITORY
// ============================================================================

#region Staff Attendance Record Repository

public class StaffAttendanceRecordRepository : GenericRepository<StaffAttendanceRecord>, IStaffAttendanceRecordRepository
{
    public StaffAttendanceRecordRepository(ApplicationDbContext context) : base(context) { }

    public async Task<StaffAttendanceRecord?> GetByEmployeeAndDateAsync(Guid employeeId, DateOnly date)
    {
        return await _dbSet
            .Include(r => r.Employee)
            .FirstOrDefaultAsync(r => r.EmployeeId == employeeId && r.Date == date && !r.IsDeleted);
    }

    public async Task<IEnumerable<StaffAttendanceRecord>> GetByEmployeeIdAsync(Guid employeeId, DateOnly from, DateOnly to)
    {
        return await _dbSet
            .Include(r => r.Employee)
            .Where(r => r.EmployeeId == employeeId && r.Date >= from && r.Date <= to && !r.IsDeleted)
            .OrderBy(r => r.Date)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffAttendanceRecord>> GetByDateAsync(DateOnly date)
    {
        return await _dbSet
            .Include(r => r.Employee)
            .Where(r => r.Date == date && !r.IsDeleted)
            .OrderBy(r => r.Employee.LastName)
            .ThenBy(r => r.Employee.FirstName)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffAttendanceRecord>> GetByDateRangeAsync(DateOnly from, DateOnly to)
    {
        return await _dbSet
            .Include(r => r.Employee)
            .Where(r => r.Date >= from && r.Date <= to && !r.IsDeleted)
            .OrderBy(r => r.Date)
            .ThenBy(r => r.Employee.LastName)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffAttendanceRecord>> GetByStatusAsync(StaffAttendanceStatus status, DateOnly from, DateOnly to)
    {
        return await _dbSet
            .Include(r => r.Employee)
            .Where(r => r.Status == status && r.Date >= from && r.Date <= to && !r.IsDeleted)
            .OrderBy(r => r.Date)
            .ThenBy(r => r.Employee.LastName)
            .ToListAsync();
    }
}

#endregion

// ============================================================================
// STAFF DAILY ATTENDANCE REPOSITORY
// ============================================================================

#region Staff Daily Attendance Repository

public class StaffDailyAttendanceRepository : GenericRepository<StaffDailyAttendance>, IStaffDailyAttendanceRepository
{
    public StaffDailyAttendanceRepository(ApplicationDbContext context) : base(context) { }

    public async Task<StaffDailyAttendance?> GetByEmployeeAndDateAsync(Guid employeeId, DateOnly date)
    {
        return await _dbSet
            .Include(a => a.Employee)
            .Include(a => a.WorkSchedule)
            .Include(a => a.Location)
            .FirstOrDefaultAsync(a => a.EmployeeId == employeeId && a.AttendanceDate == date && !a.IsDeleted);
    }

    public async Task<IEnumerable<StaffDailyAttendance>> GetByEmployeeIdAsync(Guid employeeId, DateOnly from, DateOnly to)
    {
        return await _dbSet
            .Include(a => a.Employee)
            .Include(a => a.Location)
            .Where(a => a.EmployeeId == employeeId && a.AttendanceDate >= from && a.AttendanceDate <= to && !a.IsDeleted)
            .OrderBy(a => a.AttendanceDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffDailyAttendance>> GetByDateAsync(DateOnly date)
    {
        return await _dbSet
            .Include(a => a.Employee)
            .Include(a => a.Location)
            .Where(a => a.AttendanceDate == date && !a.IsDeleted)
            .OrderBy(a => a.Employee.LastName)
            .ThenBy(a => a.Employee.FirstName)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffDailyAttendance>> GetByStatusAsync(StaffAttendanceStatus status, DateOnly from, DateOnly to)
    {
        return await _dbSet
            .Include(a => a.Employee)
            .Where(a => a.Status == status && a.AttendanceDate >= from && a.AttendanceDate <= to && !a.IsDeleted)
            .OrderBy(a => a.AttendanceDate)
            .ThenBy(a => a.Employee.LastName)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffDailyAttendance>> GetByEmployeeAndStatusAsync(Guid employeeId, StaffAttendanceStatus status, DateOnly from, DateOnly to)
    {
        return await _dbSet
            .Include(a => a.Employee)
            .Where(a => a.EmployeeId == employeeId && a.Status == status
                     && a.AttendanceDate >= from && a.AttendanceDate <= to && !a.IsDeleted)
            .OrderBy(a => a.AttendanceDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffDailyAttendance>> GetPendingVerificationAsync()
    {
        return await _dbSet
            .Include(a => a.Employee)
            .Include(a => a.Location)
            .Where(a => a.RequiresVerification && !a.IsVerified && !a.IsDeleted)
            .OrderBy(a => a.AttendanceDate)
            .ThenBy(a => a.Employee.LastName)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffDailyAttendance>> GetWithOpenExceptionsAsync()
    {
        return await _dbSet
            .Include(a => a.Employee)
            .Where(a => a.HasException && !a.ExceptionApproved && !a.IsDeleted)
            .OrderBy(a => a.AttendanceDate)
            .ThenBy(a => a.Employee.LastName)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffDailyAttendance>> GetWithOvertimeAsync(DateOnly from, DateOnly to, Guid? employeeId = null)
    {
        var query = _dbSet
            .Include(a => a.Employee)
            .Where(a => a.IsOvertime && a.AttendanceDate >= from && a.AttendanceDate <= to && !a.IsDeleted);

        if (employeeId.HasValue)
            query = query.Where(a => a.EmployeeId == employeeId.Value);

        return await query
            .OrderBy(a => a.AttendanceDate)
            .ThenBy(a => a.Employee.LastName)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffDailyAttendance>> GetLateAttendancesAsync(DateOnly from, DateOnly to, Guid? employeeId = null)
    {
        var query = _dbSet
            .Include(a => a.Employee)
            .Where(a => a.IsLate && a.AttendanceDate >= from && a.AttendanceDate <= to && !a.IsDeleted);

        if (employeeId.HasValue)
            query = query.Where(a => a.EmployeeId == employeeId.Value);

        return await query
            .OrderBy(a => a.AttendanceDate)
            .ThenBy(a => a.Employee.LastName)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffDailyAttendance>> GetRemoteWorkDaysAsync(DateOnly from, DateOnly to, Guid? employeeId = null)
    {
        var query = _dbSet
            .Include(a => a.Employee)
            .Where(a => a.IsRemoteWork && a.AttendanceDate >= from && a.AttendanceDate <= to && !a.IsDeleted);

        if (employeeId.HasValue)
            query = query.Where(a => a.EmployeeId == employeeId.Value);

        return await query
            .OrderBy(a => a.AttendanceDate)
            .ThenBy(a => a.Employee.LastName)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffDailyAttendance>> GetByPayPeriodIdAsync(Guid payPeriodId)
    {
        return await _dbSet
            .Include(a => a.Employee)
            .Where(a => a.PayPeriodId == payPeriodId && !a.IsDeleted)
            .OrderBy(a => a.AttendanceDate)
            .ThenBy(a => a.Employee.LastName)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffDailyAttendance>> GetByLocationIdAsync(Guid locationId, DateOnly from, DateOnly to)
    {
        return await _dbSet
            .Include(a => a.Employee)
            .Include(a => a.Location)
            .Where(a => a.LocationId == locationId && a.AttendanceDate >= from && a.AttendanceDate <= to && !a.IsDeleted)
            .OrderBy(a => a.AttendanceDate)
            .ThenBy(a => a.Employee.LastName)
            .ToListAsync();
    }

    public async Task<StaffDailyAttendance?> GetWithFullDetailsAsync(Guid id)
    {
        return await _dbSet
            .Include(a => a.Employee)
            .Include(a => a.WorkSchedule)
            .Include(a => a.Location)
            .Include(a => a.CheckInGeofenceZone)
            .Include(a => a.RemoteWorkRequest)
            .Include(a => a.PayPeriod)
            .Include(a => a.LeaveRequest)
            .Include(a => a.PublicHoliday)
            .Include(a => a.OvertimeApprovedBy)
            .Include(a => a.VerifiedBy)
            .Include(a => a.ExceptionApprovedBy)
            .Include(a => a.AttendanceLogs)
            .Include(a => a.Regularizations).ThenInclude(r => r.ApprovedBy)
            .FirstOrDefaultAsync(a => a.Id == id && !a.IsDeleted);
    }
}

#endregion

// ============================================================================
// STAFF ATTENDANCE LOG REPOSITORY
// ============================================================================

#region Staff Attendance Log Repository

public class StaffAttendanceLogRepository : GenericRepository<StaffAttendanceLog>, IStaffAttendanceLogRepository
{
    public StaffAttendanceLogRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<StaffAttendanceLog>> GetByEmployeeIdAsync(Guid employeeId, DateTime from, DateTime to)
    {
        return await _dbSet
            .Include(l => l.Employee)
            .Include(l => l.Device)
            .Where(l => l.EmployeeId == employeeId && l.LogDateTime >= from && l.LogDateTime <= to && !l.IsDeleted)
            .OrderBy(l => l.LogDateTime)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffAttendanceLog>> GetUnprocessedLogsAsync()
    {
        return await _dbSet
            .Include(l => l.Employee)
            .Include(l => l.Device)
            .Where(l => !l.IsProcessed && !l.IsDeleted)
            .OrderBy(l => l.LogDateTime)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffAttendanceLog>> GetUnprocessedLogsByEmployeeAsync(Guid employeeId)
    {
        return await _dbSet
            .Include(l => l.Employee)
            .Where(l => l.EmployeeId == employeeId && !l.IsProcessed && !l.IsDeleted)
            .OrderBy(l => l.LogDateTime)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffAttendanceLog>> GetByDeviceIdAsync(Guid deviceId, DateTime from, DateTime to)
    {
        return await _dbSet
            .Include(l => l.Employee)
            .Where(l => l.DeviceId == deviceId && l.LogDateTime >= from && l.LogDateTime <= to && !l.IsDeleted)
            .OrderBy(l => l.LogDateTime)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffAttendanceLog>> GetLinkedToAttendanceAsync(Guid attendanceId)
    {
        return await _dbSet
            .Include(l => l.Employee)
            .Where(l => l.AttendanceId == attendanceId && !l.IsDeleted)
            .OrderBy(l => l.LogDateTime)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffAttendanceLog>> GetByDateRangeAsync(DateTime from, DateTime to)
    {
        return await _dbSet
            .Include(l => l.Employee)
            .Include(l => l.Device)
            .Where(l => l.LogDateTime >= from && l.LogDateTime <= to && !l.IsDeleted)
            .OrderBy(l => l.LogDateTime)
            .ToListAsync();
    }
}

#endregion

// ============================================================================
// STAFF ATTENDANCE REGULARIZATION REPOSITORY
// ============================================================================

#region Staff Attendance Regularization Repository

public class StaffAttendanceRegularizationRepository : GenericRepository<StaffAttendanceRegularization>, IStaffAttendanceRegularizationRepository
{
    public StaffAttendanceRegularizationRepository(ApplicationDbContext context) : base(context) { }

    public async Task<StaffAttendanceRegularization?> GetByRegularizationNumberAsync(string regularizationNumber)
    {
        return await _dbSet
            .Include(r => r.Employee)
            .Include(r => r.Attendance)
            .Include(r => r.ApprovedBy)
            .FirstOrDefaultAsync(r => r.RegularizationNumber == regularizationNumber && !r.IsDeleted);
    }

    public async Task<IEnumerable<StaffAttendanceRegularization>> GetByEmployeeIdAsync(Guid employeeId)
    {
        return await _dbSet
            .Include(r => r.Attendance)
            .Include(r => r.ApprovedBy)
            .Where(r => r.EmployeeId == employeeId && !r.IsDeleted)
            .OrderByDescending(r => r.RequestDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffAttendanceRegularization>> GetByAttendanceIdAsync(Guid attendanceId)
    {
        return await _dbSet
            .Include(r => r.Employee)
            .Include(r => r.ApprovedBy)
            .Where(r => r.AttendanceId == attendanceId && !r.IsDeleted)
            .OrderByDescending(r => r.RequestDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffAttendanceRegularization>> GetByStatusAsync(AttendanceRegularizationStatus status)
    {
        return await _dbSet
            .Include(r => r.Employee)
            .Include(r => r.Attendance)
            .Where(r => r.Status == status && !r.IsDeleted)
            .OrderBy(r => r.RequestDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffAttendanceRegularization>> GetPendingApprovalAsync()
    {
        return await _dbSet
            .Include(r => r.Employee)
            .Include(r => r.Attendance)
            .Where(r => r.Status == AttendanceRegularizationStatus.Pending && !r.IsDeleted)
            .OrderBy(r => r.RequestDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffAttendanceRegularization>> GetAppliedAsync(DateOnly from, DateOnly to)
    {
        return await _dbSet
            .Include(r => r.Employee)
            .Include(r => r.Attendance)
            .Where(r => r.IsApplied && r.AttendanceDate >= from && r.AttendanceDate <= to && !r.IsDeleted)
            .OrderBy(r => r.AttendanceDate)
            .ThenBy(r => r.Employee.LastName)
            .ToListAsync();
    }

    public async Task<StaffAttendanceRegularization?> GetWithFullDetailsAsync(Guid id)
    {
        return await _dbSet
            .Include(r => r.Employee)
            .Include(r => r.Attendance).ThenInclude(a => a.WorkSchedule)
            .Include(r => r.ApprovedBy)
            .FirstOrDefaultAsync(r => r.Id == id && !r.IsDeleted);
    }
}

#endregion

// ============================================================================
// STAFF MONTHLY ATTENDANCE SUMMARY REPOSITORY
// ============================================================================

#region Staff Monthly Attendance Summary Repository

public class StaffMonthlyAttendanceSummaryRepository : GenericRepository<StaffMonthlyAttendanceSummary>, IStaffMonthlyAttendanceSummaryRepository
{
    public StaffMonthlyAttendanceSummaryRepository(ApplicationDbContext context) : base(context) { }

    public async Task<StaffMonthlyAttendanceSummary?> GetByEmployeeAndPeriodAsync(Guid employeeId, int year, int month)
    {
        return await _dbSet
            .Include(s => s.Employee)
            .Include(s => s.PayPeriod)
            .FirstOrDefaultAsync(s => s.EmployeeId == employeeId && s.Year == year && s.Month == month && !s.IsDeleted);
    }

    public async Task<IEnumerable<StaffMonthlyAttendanceSummary>> GetByEmployeeAndYearAsync(Guid employeeId, int year)
    {
        return await _dbSet
            .Include(s => s.PayPeriod)
            .Where(s => s.EmployeeId == employeeId && s.Year == year && !s.IsDeleted)
            .OrderBy(s => s.Month)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffMonthlyAttendanceSummary>> GetByYearAndMonthAsync(int year, int month)
    {
        return await _dbSet
            .Include(s => s.Employee)
            .Where(s => s.Year == year && s.Month == month && !s.IsDeleted)
            .OrderBy(s => s.Employee.LastName)
            .ThenBy(s => s.Employee.FirstName)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffMonthlyAttendanceSummary>> GetByPayPeriodIdAsync(Guid payPeriodId)
    {
        return await _dbSet
            .Include(s => s.Employee)
            .Where(s => s.PayPeriodId == payPeriodId && !s.IsDeleted)
            .OrderBy(s => s.Employee.LastName)
            .ThenBy(s => s.Employee.FirstName)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffMonthlyAttendanceSummary>> GetUnfinalizedAsync(int year, int month)
    {
        return await _dbSet
            .Include(s => s.Employee)
            .Where(s => s.Year == year && s.Month == month && !s.IsFinalized && !s.IsDeleted)
            .OrderBy(s => s.Employee.LastName)
            .ToListAsync();
    }
}

#endregion

// ============================================================================
// STAFF BULK ATTENDANCE IMPORT REPOSITORY
// ============================================================================

#region Staff Bulk Attendance Import Repository

public class StaffBulkAttendanceImportRepository : GenericRepository<StaffBulkAttendanceImport>, IStaffBulkAttendanceImportRepository
{
    public StaffBulkAttendanceImportRepository(ApplicationDbContext context) : base(context) { }

    public async Task<StaffBulkAttendanceImport?> GetByImportReferenceAsync(string importReference)
    {
        return await _dbSet
            .Include(i => i.ImportedBy)
            .FirstOrDefaultAsync(i => i.ImportReference == importReference && !i.IsDeleted);
    }

    public async Task<IEnumerable<StaffBulkAttendanceImport>> GetByStatusAsync(AttendanceImportStatus status)
    {
        return await _dbSet
            .Include(i => i.ImportedBy)
            .Where(i => i.Status == status && !i.IsDeleted)
            .OrderByDescending(i => i.ImportDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffBulkAttendanceImport>> GetByImportedByAsync(Guid employeeId)
    {
        return await _dbSet
            .Include(i => i.ImportedBy)
            .Where(i => i.ImportedById == employeeId && !i.IsDeleted)
            .OrderByDescending(i => i.ImportDate)
            .ToListAsync();
    }

    public async Task<StaffBulkAttendanceImport?> GetWithRowsAsync(Guid id)
    {
        return await _dbSet
            .Include(i => i.ImportedBy)
            .Include(i => i.ImportRows)
            .FirstOrDefaultAsync(i => i.Id == id && !i.IsDeleted);
    }
}

#endregion

// ============================================================================
// STAFF BULK ATTENDANCE IMPORT ROW REPOSITORY
// ============================================================================

#region Staff Bulk Attendance Import Row Repository

public class StaffBulkAttendanceImportRowRepository : GenericRepository<StaffBulkAttendanceImportRow>, IStaffBulkAttendanceImportRowRepository
{
    public StaffBulkAttendanceImportRowRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<StaffBulkAttendanceImportRow>> GetByImportIdAsync(Guid importId)
    {
        return await _dbSet
            .Include(r => r.Employee)
            .Where(r => r.ImportId == importId && !r.IsDeleted)
            .OrderBy(r => r.RowNumber)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffBulkAttendanceImportRow>> GetFailedRowsAsync(Guid importId)
    {
        return await _dbSet
            .Include(r => r.Employee)
            .Where(r => r.ImportId == importId && !r.IsSuccess && !r.IsDeleted)
            .OrderBy(r => r.RowNumber)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffBulkAttendanceImportRow>> GetByEmployeeIdAsync(Guid employeeId)
    {
        return await _dbSet
            .Include(r => r.Import)
            .Where(r => r.EmployeeId == employeeId && !r.IsDeleted)
            .OrderByDescending(r => r.Import.ImportDate)
            .ThenBy(r => r.RowNumber)
            .ToListAsync();
    }
}

#endregion
