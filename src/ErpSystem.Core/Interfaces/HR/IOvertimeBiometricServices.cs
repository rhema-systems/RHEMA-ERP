using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Interfaces.HR;

// ============================================================================
// POSITION OVERTIME POLICY SERVICE
// ============================================================================

#region Position Overtime Policy Service

public interface IPositionOvertimePolicyService
{
    Task<PositionOvertimePolicyDto> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IEnumerable<PositionOvertimePolicyDto>> GetByPositionIdAsync(Guid positionId, CancellationToken ct = default);
    Task<IEnumerable<PositionOvertimePolicyDto>> GetByAllowanceTypeAsync(OvertimeAllowanceType allowanceType, CancellationToken ct = default);
    Task<PositionOvertimePolicyDto?> GetActiveForPositionAsync(Guid positionId, OvertimeAllowanceType allowanceType, CancellationToken ct = default);

    Task<PositionOvertimePolicyDto> CreateAsync(CreatePositionOvertimePolicyDto dto, Guid tenantId, Guid userId, CancellationToken ct = default);
    Task<PositionOvertimePolicyDto> UpdateAsync(UpdatePositionOvertimePolicyDto dto, Guid userId, CancellationToken ct = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken ct = default);

    // Override sub-operations
    Task<EmployeeOvertimeOverrideDto> AddOverrideAsync(CreateEmployeeOvertimeOverrideDto dto, Guid tenantId, Guid userId, CancellationToken ct = default);
    Task<IEnumerable<EmployeeOvertimeOverrideDto>> GetOverridesAsync(Guid policyId, CancellationToken ct = default);
    Task<EmployeeOvertimeOverrideDto> UpdateOverrideAsync(UpdateEmployeeOvertimeOverrideDto dto, Guid userId, CancellationToken ct = default);
    Task<bool> DeleteOverrideAsync(Guid overrideId, CancellationToken ct = default);
}

#endregion

// ============================================================================
// EMPLOYEE OVERTIME OVERRIDE SERVICE
// ============================================================================

#region Employee Overtime Override Service

public interface IEmployeeOvertimeOverrideService
{
    Task<EmployeeOvertimeOverrideDto> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IEnumerable<EmployeeOvertimeOverrideDto>> GetByEmployeeIdAsync(Guid employeeId, CancellationToken ct = default);
    Task<IEnumerable<EmployeeOvertimeOverrideDto>> GetActiveOverridesForEmployeeAsync(Guid employeeId, CancellationToken ct = default);

    Task<EmployeeOvertimeOverrideDto> CreateAsync(CreateEmployeeOvertimeOverrideDto dto, Guid tenantId, Guid userId, CancellationToken ct = default);
    Task<EmployeeOvertimeOverrideDto> UpdateAsync(UpdateEmployeeOvertimeOverrideDto dto, Guid userId, CancellationToken ct = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken ct = default);
}

#endregion

// ============================================================================
// STAFF OVERTIME REQUEST SERVICE
// ============================================================================

#region Staff Overtime Request Service

public interface IStaffOvertimeRequestService
{
    Task<StaffOvertimeRequestDto> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<StaffOvertimeRequestDto?> GetByRequestNumberAsync(string requestNumber, CancellationToken ct = default);
    Task<IEnumerable<StaffOvertimeRequestSummaryDto>> GetByEmployeeIdAsync(Guid employeeId, CancellationToken ct = default);
    Task<IEnumerable<StaffOvertimeRequestSummaryDto>> GetByStatusAsync(OvertimeRequestStatus status, CancellationToken ct = default);
    Task<IEnumerable<StaffOvertimeRequestSummaryDto>> GetPendingApprovalAsync(CancellationToken ct = default);
    Task<IEnumerable<StaffOvertimeRequestSummaryDto>> GetPendingSupervisorConfirmationAsync(CancellationToken ct = default);
    Task<IEnumerable<StaffOvertimeRequestSummaryDto>> GetByDateRangeAsync(DateOnly from, DateOnly to, CancellationToken ct = default);
    Task<PagedResult<StaffOvertimeRequestSummaryDto>> GetPagedAsync(int pageNumber, int pageSize, CancellationToken ct = default);

    Task<StaffOvertimeRequestDto> CreateAsync(CreateStaffOvertimeRequestDto dto, Guid tenantId, Guid userId, CancellationToken ct = default);
    Task<StaffOvertimeRequestDto> UpdateAsync(UpdateStaffOvertimeRequestDto dto, Guid userId, CancellationToken ct = default);
    Task<StaffOvertimeRequestDto> ApproveAsync(Guid requestId, string? comments, Guid userId, CancellationToken ct = default);
    Task<StaffOvertimeRequestDto> RejectAsync(Guid requestId, string rejectionReason, Guid userId, CancellationToken ct = default);
    Task<StaffOvertimeRequestDto> ConfirmActualHoursAsync(Guid requestId, decimal actualHours, string? supervisorNotes, Guid userId, CancellationToken ct = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken ct = default);
}

#endregion

// ============================================================================
// EMPLOYEE BIOMETRIC SERVICE
// ============================================================================

#region Employee Biometric Service

public interface IEmployeeBiometricService
{
    Task<EmployeeBiometricDto> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IEnumerable<EmployeeBiometricSummaryDto>> GetByEmployeeIdAsync(Guid employeeId, CancellationToken ct = default);
    Task<IEnumerable<EmployeeBiometricSummaryDto>> GetActiveBiometricsForEmployeeAsync(Guid employeeId, CancellationToken ct = default);
    Task<EmployeeBiometricDto?> GetByEmployeeAndTypeAsync(Guid employeeId, BiometricType type, CancellationToken ct = default);
    Task<IEnumerable<EmployeeBiometricSummaryDto>> GetByTypeAsync(BiometricType type, CancellationToken ct = default);
    Task<PagedResult<EmployeeBiometricSummaryDto>> GetPagedAsync(int pageNumber, int pageSize, CancellationToken ct = default);

    Task<EmployeeBiometricDto> EnrolAsync(EnrollBiometricDto dto, Guid tenantId, Guid userId, CancellationToken ct = default);
    Task<EmployeeBiometricDto> UpdateAsync(UpdateEmployeeBiometricDto dto, Guid userId, CancellationToken ct = default);
    Task<bool> RevokeAsync(Guid id, string reason, Guid userId, CancellationToken ct = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken ct = default);
}

#endregion

// ============================================================================
// STAFF ATTENDANCE DEVICE SERVICE
// ============================================================================

#region Staff Attendance Device Service

public interface IStaffAttendanceDeviceService
{
    Task<StaffAttendanceDeviceDto> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<StaffAttendanceDeviceDto?> GetByExternalDeviceIdAsync(string externalDeviceId, CancellationToken ct = default);
    Task<IEnumerable<StaffAttendanceDeviceSummaryDto>> GetAllAsync(CancellationToken ct = default);
    Task<IEnumerable<StaffAttendanceDeviceSummaryDto>> GetActiveDevicesAsync(CancellationToken ct = default);
    Task<IEnumerable<StaffAttendanceDeviceSummaryDto>> GetByLocationIdAsync(Guid locationId, CancellationToken ct = default);
    Task<IEnumerable<StaffAttendanceDeviceSummaryDto>> GetDevicesOverdueForSyncAsync(int hoursThreshold = 24, CancellationToken ct = default);
    Task<PagedResult<StaffAttendanceDeviceSummaryDto>> GetPagedAsync(int pageNumber, int pageSize, CancellationToken ct = default);

    Task<StaffAttendanceDeviceDto> RegisterAsync(CreateStaffAttendanceDeviceDto dto, Guid tenantId, Guid userId, CancellationToken ct = default);
    Task<StaffAttendanceDeviceDto> UpdateAsync(UpdateStaffAttendanceDeviceDto dto, Guid userId, CancellationToken ct = default);
    Task<StaffAttendanceDeviceDto> RecordSyncAsync(Guid deviceId, int? pendingSyncCount, Guid userId, CancellationToken ct = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken ct = default);
}

#endregion
