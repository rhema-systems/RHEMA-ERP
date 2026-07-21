using ErpSystem.Core.Entities.HR.StaffAttendance;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Data.Repositories.HR;

// ============================================================================
// POSITION OVERTIME POLICY REPOSITORY
// ============================================================================

#region Position Overtime Policy Repository

public class PositionOvertimePolicyRepository : GenericRepository<PositionOvertimePolicy>, IPositionOvertimePolicyRepository
{
    public PositionOvertimePolicyRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<PositionOvertimePolicy>> GetByPositionIdAsync(Guid positionId)
    {
        return await _dbSet
            .Include(p => p.Position)
            .Where(p => p.PositionId == positionId && !p.IsDeleted)
            .OrderBy(p => p.AllowanceType)
            .ThenByDescending(p => p.EffectiveDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<PositionOvertimePolicy>> GetByAllowanceTypeAsync(OvertimeAllowanceType allowanceType)
    {
        return await _dbSet
            .Include(p => p.Position)
            .Where(p => p.AllowanceType == allowanceType && !p.IsDeleted)
            .OrderBy(p => p.Position.Title)
            .ToListAsync();
    }

    public async Task<PositionOvertimePolicy?> GetActiveForPositionAsync(Guid positionId, OvertimeAllowanceType allowanceType)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        return await _dbSet
            .Include(p => p.Position)
            .Where(p => p.PositionId == positionId
                     && p.AllowanceType == allowanceType
                     && p.EffectiveDate <= today
                     && (p.ExpiryDate == null || p.ExpiryDate >= today)
                     && !p.IsDeleted)
            .OrderByDescending(p => p.EffectiveDate)
            .FirstOrDefaultAsync();
    }

    public async Task<PositionOvertimePolicy?> GetWithOverridesAsync(Guid id)
    {
        return await _dbSet
            .Include(p => p.Position)
            .Include(p => p.EmployeeOverrides).ThenInclude(o => o.Employee)
            .Include(p => p.EmployeeOverrides).ThenInclude(o => o.ApprovedBy)
            .FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted);
    }
}

#endregion

// ============================================================================
// EMPLOYEE OVERTIME OVERRIDE REPOSITORY
// ============================================================================

#region Employee Overtime Override Repository

public class EmployeeOvertimeOverrideRepository : GenericRepository<EmployeeOvertimeOverride>, IEmployeeOvertimeOverrideRepository
{
    public EmployeeOvertimeOverrideRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<EmployeeOvertimeOverride>> GetByEmployeeIdAsync(Guid employeeId)
    {
        return await _dbSet
            .Include(o => o.Policy)
            .Include(o => o.ApprovedBy)
            .Where(o => o.EmployeeId == employeeId && !o.IsDeleted)
            .OrderByDescending(o => o.EffectiveDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<EmployeeOvertimeOverride>> GetActiveOverridesForEmployeeAsync(Guid employeeId)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        return await _dbSet
            .Include(o => o.Policy)
            .Include(o => o.ApprovedBy)
            .Where(o => o.EmployeeId == employeeId
                     && o.EffectiveDate <= today
                     && (o.ExpiryDate == null || o.ExpiryDate >= today)
                     && !o.IsDeleted)
            .OrderBy(o => o.AllowanceType)
            .ToListAsync();
    }

    public async Task<IEnumerable<EmployeeOvertimeOverride>> GetByPolicyIdAsync(Guid policyId)
    {
        return await _dbSet
            .Include(o => o.Employee)
            .Include(o => o.ApprovedBy)
            .Where(o => o.PolicyId == policyId && !o.IsDeleted)
            .OrderBy(o => o.Employee.LastName)
            .ThenBy(o => o.Employee.FirstName)
            .ToListAsync();
    }
}

#endregion

// ============================================================================
// STAFF OVERTIME REQUEST REPOSITORY
// ============================================================================

#region Staff Overtime Request Repository

public class StaffOvertimeRequestRepository : GenericRepository<StaffOvertimeRequest>, IStaffOvertimeRequestRepository
{
    public StaffOvertimeRequestRepository(ApplicationDbContext context) : base(context) { }

    public async Task<StaffOvertimeRequest?> GetByRequestNumberAsync(string requestNumber)
    {
        return await _dbSet
            .Include(r => r.Employee)
            .Include(r => r.ApprovedBy)
            .Include(r => r.SupervisorConfirmedBy)
            .FirstOrDefaultAsync(r => r.RequestNumber == requestNumber && !r.IsDeleted);
    }

    public async Task<IEnumerable<StaffOvertimeRequest>> GetByEmployeeIdAsync(Guid employeeId)
    {
        return await _dbSet
            .Include(r => r.ApprovedBy)
            .Where(r => r.EmployeeId == employeeId && !r.IsDeleted)
            .OrderByDescending(r => r.OvertimeDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffOvertimeRequest>> GetByStatusAsync(OvertimeRequestStatus status)
    {
        return await _dbSet
            .Include(r => r.Employee)
            .Include(r => r.ApprovedBy)
            .Where(r => r.Status == status && !r.IsDeleted)
            .OrderBy(r => r.OvertimeDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffOvertimeRequest>> GetPendingApprovalAsync()
    {
        return await _dbSet
            .Include(r => r.Employee)
            .Where(r => r.Status == OvertimeRequestStatus.Pending && !r.IsDeleted)
            .OrderBy(r => r.OvertimeDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffOvertimeRequest>> GetPendingSupervisorConfirmationAsync()
    {
        return await _dbSet
            .Include(r => r.Employee)
            .Include(r => r.ApprovedBy)
            .Where(r => r.Status == OvertimeRequestStatus.Approved
                     && r.ActualOvertimeHours == null
                     && !r.IsDeleted)
            .OrderBy(r => r.OvertimeDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffOvertimeRequest>> GetByDateRangeAsync(DateOnly from, DateOnly to)
    {
        var fromDate = from.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var toDate = to.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc);
        return await _dbSet
            .Include(r => r.Employee)
            .Where(r => r.OvertimeDate >= fromDate && r.OvertimeDate <= toDate && !r.IsDeleted)
            .OrderBy(r => r.OvertimeDate)
            .ThenBy(r => r.Employee.LastName)
            .ToListAsync();
    }

    public async Task<StaffOvertimeRequest?> GetWithFullDetailsAsync(Guid id)
    {
        return await _dbSet
            .Include(r => r.Employee)
            .Include(r => r.ApprovedBy)
            .Include(r => r.SupervisorConfirmedBy)
            .Include(r => r.Attendance)
            .FirstOrDefaultAsync(r => r.Id == id && !r.IsDeleted);
    }
}

#endregion

// ============================================================================
// EMPLOYEE BIOMETRIC REPOSITORY
// ============================================================================

#region Employee Biometric Repository

public class EmployeeBiometricRepository : GenericRepository<EmployeeBiometric>, IEmployeeBiometricRepository
{
    public EmployeeBiometricRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<EmployeeBiometric>> GetByEmployeeIdAsync(Guid employeeId)
    {
        return await _dbSet
            .Include(b => b.EnrolledBy)
            .Where(b => b.EmployeeId == employeeId && !b.IsDeleted)
            .OrderBy(b => b.BiometricType)
            .ThenByDescending(b => b.EnrolledDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<EmployeeBiometric>> GetActiveBiometricsForEmployeeAsync(Guid employeeId)
    {
        return await _dbSet
            .Where(b => b.EmployeeId == employeeId && b.IsActive && !b.IsDeleted)
            .OrderBy(b => b.BiometricType)
            .ToListAsync();
    }

    public async Task<EmployeeBiometric?> GetByEmployeeAndTypeAsync(Guid employeeId, BiometricType type)
    {
        return await _dbSet
            .Include(b => b.EnrolledBy)
            .FirstOrDefaultAsync(b => b.EmployeeId == employeeId && b.BiometricType == type && b.IsActive && !b.IsDeleted);
    }

    public async Task<IEnumerable<EmployeeBiometric>> GetByTypeAsync(BiometricType type)
    {
        return await _dbSet
            .Include(b => b.Employee)
            .Where(b => b.BiometricType == type && b.IsActive && !b.IsDeleted)
            .OrderBy(b => b.Employee.LastName)
            .ThenBy(b => b.Employee.FirstName)
            .ToListAsync();
    }

    public async Task<IEnumerable<EmployeeBiometric>> GetRevokedBiometricsAsync()
    {
        return await _dbSet
            .Include(b => b.Employee)
            .Where(b => !b.IsActive && b.RevokedDate != null && !b.IsDeleted)
            .OrderByDescending(b => b.RevokedDate)
            .ToListAsync();
    }
}

#endregion

// ============================================================================
// STAFF ATTENDANCE DEVICE REPOSITORY
// ============================================================================

#region Staff Attendance Device Repository

public class StaffAttendanceDeviceRepository : GenericRepository<StaffAttendanceDevice>, IStaffAttendanceDeviceRepository
{
    public StaffAttendanceDeviceRepository(ApplicationDbContext context) : base(context) { }

    public async Task<StaffAttendanceDevice?> GetByExternalDeviceIdAsync(string externalDeviceId)
    {
        return await _dbSet
            .Include(d => d.Location)
            .FirstOrDefaultAsync(d => d.DeviceId == externalDeviceId && !d.IsDeleted);
    }

    public async Task<IEnumerable<StaffAttendanceDevice>> GetActiveDevicesAsync()
    {
        return await _dbSet
            .Include(d => d.Location)
            .Where(d => d.IsActive && !d.IsDeleted)
            .OrderBy(d => d.DeviceName)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffAttendanceDevice>> GetByLocationIdAsync(Guid locationId)
    {
        return await _dbSet
            .Include(d => d.Location)
            .Where(d => d.LocationId == locationId && d.IsActive && !d.IsDeleted)
            .OrderBy(d => d.DeviceName)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffAttendanceDevice>> GetDevicesOverdueForSyncAsync(int hoursThreshold = 24)
    {
        var cutoff = DateTime.UtcNow.AddHours(-hoursThreshold);
        return await _dbSet
            .Include(d => d.Location)
            .Where(d => d.IsActive && !d.IsDeleted
                     && (d.LastSyncDate == null || d.LastSyncDate < cutoff))
            .OrderBy(d => d.LastSyncDate)
            .ToListAsync();
    }
}

#endregion
