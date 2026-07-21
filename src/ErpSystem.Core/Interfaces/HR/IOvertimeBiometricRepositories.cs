using ErpSystem.Core.Entities.HR.StaffAttendance;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Interfaces.HR;

// ============================================================================
// POSITION OVERTIME POLICY
// ============================================================================

#region Position Overtime Policy

public interface IPositionOvertimePolicyRepository : IGenericRepository<PositionOvertimePolicy>
{
    /// <summary>Returns all overtime policies defined for a specific position.</summary>
    Task<IEnumerable<PositionOvertimePolicy>> GetByPositionIdAsync(Guid positionId);

    /// <summary>Returns all policies of a given allowance type across all positions.</summary>
    Task<IEnumerable<PositionOvertimePolicy>> GetByAllowanceTypeAsync(OvertimeAllowanceType allowanceType);

    /// <summary>
    /// Returns the currently active policy for a position and allowance type
    /// (EffectiveDate ≤ today and ExpiryDate is null or ≥ today).
    /// </summary>
    Task<PositionOvertimePolicy?> GetActiveForPositionAsync(Guid positionId, OvertimeAllowanceType allowanceType);

    /// <summary>Returns a policy fully loaded with its employee-level overrides.</summary>
    Task<PositionOvertimePolicy?> GetWithOverridesAsync(Guid id);
}

#endregion

// ============================================================================
// EMPLOYEE OVERTIME OVERRIDE
// ============================================================================

#region Employee Overtime Override

public interface IEmployeeOvertimeOverrideRepository : IGenericRepository<EmployeeOvertimeOverride>
{
    /// <summary>Returns all overtime overrides defined for an employee, newest first.</summary>
    Task<IEnumerable<EmployeeOvertimeOverride>> GetByEmployeeIdAsync(Guid employeeId);

    /// <summary>Returns currently active overrides for an employee (within effective/expiry date window).</summary>
    Task<IEnumerable<EmployeeOvertimeOverride>> GetActiveOverridesForEmployeeAsync(Guid employeeId);

    /// <summary>Returns all employee-level overrides scoped to a specific position policy.</summary>
    Task<IEnumerable<EmployeeOvertimeOverride>> GetByPolicyIdAsync(Guid policyId);
}

#endregion

// ============================================================================
// STAFF OVERTIME REQUEST
// ============================================================================

#region Staff Overtime Request

public interface IStaffOvertimeRequestRepository : IGenericRepository<StaffOvertimeRequest>
{
    /// <summary>Returns the overtime request matching the unique request number, or null.</summary>
    Task<StaffOvertimeRequest?> GetByRequestNumberAsync(string requestNumber);

    /// <summary>Returns all overtime requests submitted by an employee, newest first.</summary>
    Task<IEnumerable<StaffOvertimeRequest>> GetByEmployeeIdAsync(Guid employeeId);

    /// <summary>Returns overtime requests filtered by status.</summary>
    Task<IEnumerable<StaffOvertimeRequest>> GetByStatusAsync(OvertimeRequestStatus status);

    /// <summary>Returns all requests with Pending status awaiting manager pre-approval.</summary>
    Task<IEnumerable<StaffOvertimeRequest>> GetPendingApprovalAsync();

    /// <summary>
    /// Returns approved requests where the supervisor has not yet confirmed actual hours
    /// (ActualOvertimeHours is null).
    /// </summary>
    Task<IEnumerable<StaffOvertimeRequest>> GetPendingSupervisorConfirmationAsync();

    /// <summary>Returns overtime requests whose overtime date falls within the specified range.</summary>
    Task<IEnumerable<StaffOvertimeRequest>> GetByDateRangeAsync(DateOnly from, DateOnly to);

    /// <summary>Returns a fully-loaded overtime request including employee, approver, and linked attendance details.</summary>
    Task<StaffOvertimeRequest?> GetWithFullDetailsAsync(Guid id);
}

#endregion

// ============================================================================
// EMPLOYEE BIOMETRIC
// ============================================================================

#region Employee Biometric

public interface IEmployeeBiometricRepository : IGenericRepository<EmployeeBiometric>
{
    /// <summary>Returns all biometric templates enrolled for an employee.</summary>
    Task<IEnumerable<EmployeeBiometric>> GetByEmployeeIdAsync(Guid employeeId);

    /// <summary>Returns only the active (IsActive = true) biometric templates for an employee.</summary>
    Task<IEnumerable<EmployeeBiometric>> GetActiveBiometricsForEmployeeAsync(Guid employeeId);

    /// <summary>Returns the active biometric template of a specific type for an employee, or null.</summary>
    Task<EmployeeBiometric?> GetByEmployeeAndTypeAsync(Guid employeeId, BiometricType type);

    /// <summary>Returns all enrolled templates of a given biometric type across all employees.</summary>
    Task<IEnumerable<EmployeeBiometric>> GetByTypeAsync(BiometricType type);

    /// <summary>Returns all revoked biometric templates for audit purposes.</summary>
    Task<IEnumerable<EmployeeBiometric>> GetRevokedBiometricsAsync();
}

#endregion

// ============================================================================
// STAFF ATTENDANCE DEVICE
// ============================================================================

#region Staff Attendance Device

public interface IStaffAttendanceDeviceRepository : IGenericRepository<StaffAttendanceDevice>
{
    /// <summary>Returns the device matching the external device ID string (manufacturer identifier), or null.</summary>
    Task<StaffAttendanceDevice?> GetByExternalDeviceIdAsync(string externalDeviceId);

    /// <summary>Returns all active attendance devices.</summary>
    Task<IEnumerable<StaffAttendanceDevice>> GetActiveDevicesAsync();

    /// <summary>Returns all devices installed at a specific location.</summary>
    Task<IEnumerable<StaffAttendanceDevice>> GetByLocationIdAsync(Guid locationId);

    /// <summary>
    /// Returns devices that are overdue for synchronisation — either never synced or
    /// last synced more than <paramref name="hoursThreshold"/> hours ago.
    /// </summary>
    Task<IEnumerable<StaffAttendanceDevice>> GetDevicesOverdueForSyncAsync(int hoursThreshold = 24);
}

#endregion
