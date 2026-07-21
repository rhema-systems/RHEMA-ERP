using ErpSystem.Application.HR.Extensions;
using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR;

// ============================================================================
// POSITION OVERTIME POLICY SERVICE
// ============================================================================

#region Position Overtime Policy Service

public class PositionOvertimePolicyService : IPositionOvertimePolicyService
{
    private readonly IPositionOvertimePolicyRepository _repository;
    private readonly IEmployeeOvertimeOverrideRepository _overrideRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<PositionOvertimePolicyService> _logger;

    public PositionOvertimePolicyService(
        IPositionOvertimePolicyRepository repository,
        IEmployeeOvertimeOverrideRepository overrideRepository,
        IUnitOfWork unitOfWork,
        ILogger<PositionOvertimePolicyService> logger)
    {
        _repository = repository;
        _overrideRepository = overrideRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<PositionOvertimePolicyDto> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await _repository.GetByIdAsync(id);
        if (entity == null)
            throw new ArgumentException($"Overtime policy '{id}' not found.");
        return entity.ToDto();
    }

    public async Task<IEnumerable<PositionOvertimePolicyDto>> GetByPositionIdAsync(Guid positionId, CancellationToken ct = default)
    {
        var entities = await _repository.GetByPositionIdAsync(positionId);
        return entities.Select(e => e.ToDto()).ToList();
    }

    public async Task<IEnumerable<PositionOvertimePolicyDto>> GetByAllowanceTypeAsync(OvertimeAllowanceType allowanceType, CancellationToken ct = default)
    {
        var entities = await _repository.GetByAllowanceTypeAsync(allowanceType);
        return entities.Select(e => e.ToDto()).ToList();
    }

    public async Task<PositionOvertimePolicyDto?> GetActiveForPositionAsync(Guid positionId, OvertimeAllowanceType allowanceType, CancellationToken ct = default)
    {
        var entity = await _repository.GetActiveForPositionAsync(positionId, allowanceType);
        return entity?.ToDto();
    }

    public async Task<PositionOvertimePolicyDto> CreateAsync(CreatePositionOvertimePolicyDto dto, Guid tenantId, Guid userId, CancellationToken ct = default)
    {
        var entity = dto.ToEntity(tenantId, userId);
        await _repository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(ct);

        _logger.LogInformation("Overtime policy created for position {PositionId}", dto.PositionId);
        return entity.ToDto();
    }

    public async Task<PositionOvertimePolicyDto> UpdateAsync(UpdatePositionOvertimePolicyDto dto, Guid userId, CancellationToken ct = default)
    {
        var entity = await _repository.GetByIdAsync(dto.Id);
        if (entity == null)
            throw new ArgumentException($"Overtime policy '{dto.Id}' not found.");

        entity.UpdateEntity(dto, userId);
        await _repository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(ct);
        return entity.ToDto();
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await _repository.GetByIdAsync(id);
        if (entity == null)
            throw new ArgumentException($"Overtime policy '{id}' not found.");

        await _repository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(ct);
        return true;
    }

    public async Task<EmployeeOvertimeOverrideDto> AddOverrideAsync(CreateEmployeeOvertimeOverrideDto dto, Guid tenantId, Guid userId, CancellationToken ct = default)
    {
        var entity = dto.ToEntity(tenantId, userId);
        await _overrideRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(ct);
        return entity.ToDto();
    }

    public async Task<IEnumerable<EmployeeOvertimeOverrideDto>> GetOverridesAsync(Guid policyId, CancellationToken ct = default)
    {
        var entities = await _overrideRepository.GetByPolicyIdAsync(policyId);
        return entities.Select(e => e.ToDto()).ToList();
    }

    public async Task<EmployeeOvertimeOverrideDto> UpdateOverrideAsync(UpdateEmployeeOvertimeOverrideDto dto, Guid userId, CancellationToken ct = default)
    {
        var entity = await _overrideRepository.GetByIdAsync(dto.Id);
        if (entity == null)
            throw new ArgumentException($"Overtime override '{dto.Id}' not found.");

        entity.UpdateEntity(dto, userId);
        await _overrideRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(ct);
        return entity.ToDto();
    }

    public async Task<bool> DeleteOverrideAsync(Guid overrideId, CancellationToken ct = default)
    {
        var entity = await _overrideRepository.GetByIdAsync(overrideId);
        if (entity == null)
            throw new ArgumentException($"Overtime override '{overrideId}' not found.");

        await _overrideRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(ct);
        return true;
    }
}

#endregion

// ============================================================================
// EMPLOYEE OVERTIME OVERRIDE SERVICE
// ============================================================================

#region Employee Overtime Override Service

public class EmployeeOvertimeOverrideService : IEmployeeOvertimeOverrideService
{
    private readonly IEmployeeOvertimeOverrideRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<EmployeeOvertimeOverrideService> _logger;

    public EmployeeOvertimeOverrideService(
        IEmployeeOvertimeOverrideRepository repository,
        IUnitOfWork unitOfWork,
        ILogger<EmployeeOvertimeOverrideService> logger)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<EmployeeOvertimeOverrideDto> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await _repository.GetByIdAsync(id);
        if (entity == null)
            throw new ArgumentException($"Overtime override '{id}' not found.");
        return entity.ToDto();
    }

    public async Task<IEnumerable<EmployeeOvertimeOverrideDto>> GetByEmployeeIdAsync(Guid employeeId, CancellationToken ct = default)
    {
        var entities = await _repository.GetByEmployeeIdAsync(employeeId);
        return entities.Select(e => e.ToDto()).ToList();
    }

    public async Task<IEnumerable<EmployeeOvertimeOverrideDto>> GetActiveOverridesForEmployeeAsync(Guid employeeId, CancellationToken ct = default)
    {
        var entities = await _repository.GetActiveOverridesForEmployeeAsync(employeeId);
        return entities.Select(e => e.ToDto()).ToList();
    }

    public async Task<EmployeeOvertimeOverrideDto> CreateAsync(CreateEmployeeOvertimeOverrideDto dto, Guid tenantId, Guid userId, CancellationToken ct = default)
    {
        var entity = dto.ToEntity(tenantId, userId);
        await _repository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(ct);

        _logger.LogInformation("Overtime override created for employee {EmployeeId}", dto.EmployeeId);
        return entity.ToDto();
    }

    public async Task<EmployeeOvertimeOverrideDto> UpdateAsync(UpdateEmployeeOvertimeOverrideDto dto, Guid userId, CancellationToken ct = default)
    {
        var entity = await _repository.GetByIdAsync(dto.Id);
        if (entity == null)
            throw new ArgumentException($"Overtime override '{dto.Id}' not found.");

        entity.UpdateEntity(dto, userId);
        await _repository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(ct);
        return entity.ToDto();
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await _repository.GetByIdAsync(id);
        if (entity == null)
            throw new ArgumentException($"Overtime override '{id}' not found.");

        await _repository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(ct);
        return true;
    }
}

#endregion

// ============================================================================
// STAFF OVERTIME REQUEST SERVICE
// ============================================================================

#region Staff Overtime Request Service

public class StaffOvertimeRequestService : IStaffOvertimeRequestService
{
    private readonly IStaffOvertimeRequestRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<StaffOvertimeRequestService> _logger;

    public StaffOvertimeRequestService(
        IStaffOvertimeRequestRepository repository,
        IUnitOfWork unitOfWork,
        ILogger<StaffOvertimeRequestService> logger)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<StaffOvertimeRequestDto> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await _repository.GetWithFullDetailsAsync(id);
        if (entity == null)
            throw new ArgumentException($"Overtime request '{id}' not found.");
        return entity.ToDto();
    }

    public async Task<StaffOvertimeRequestDto?> GetByRequestNumberAsync(string requestNumber, CancellationToken ct = default)
    {
        var entity = await _repository.GetByRequestNumberAsync(requestNumber);
        return entity?.ToDto();
    }

    public async Task<IEnumerable<StaffOvertimeRequestSummaryDto>> GetByEmployeeIdAsync(Guid employeeId, CancellationToken ct = default)
    {
        var entities = await _repository.GetByEmployeeIdAsync(employeeId);
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<StaffOvertimeRequestSummaryDto>> GetByStatusAsync(OvertimeRequestStatus status, CancellationToken ct = default)
    {
        var entities = await _repository.GetByStatusAsync(status);
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<StaffOvertimeRequestSummaryDto>> GetPendingApprovalAsync(CancellationToken ct = default)
    {
        var entities = await _repository.GetPendingApprovalAsync();
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<StaffOvertimeRequestSummaryDto>> GetPendingSupervisorConfirmationAsync(CancellationToken ct = default)
    {
        var entities = await _repository.GetPendingSupervisorConfirmationAsync();
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<StaffOvertimeRequestSummaryDto>> GetByDateRangeAsync(DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        var entities = await _repository.GetByDateRangeAsync(from, to);
        return entities.ToSummaryDtoList();
    }

    public async Task<PagedResult<StaffOvertimeRequestSummaryDto>> GetPagedAsync(int pageNumber, int pageSize, CancellationToken ct = default)
    {
        var query = _repository.GetQueryable();
        var totalCount = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(r => r.OvertimeDate)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new PagedResult<StaffOvertimeRequestSummaryDto>
        {
            Items = items.ToSummaryDtoList(),
            TotalCount = totalCount,
            Page = pageNumber,
            PageSize = pageSize
        };
    }

    public async Task<StaffOvertimeRequestDto> CreateAsync(CreateStaffOvertimeRequestDto dto, Guid tenantId, Guid userId, CancellationToken ct = default)
    {
        var entity = dto.ToEntity(tenantId, userId);
        entity.RequestNumber = await GenerateRequestNumberAsync(ct);
        entity.Status = OvertimeRequestStatus.Pending;
        await _repository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(ct);

        _logger.LogInformation("Overtime request {Number} created for employee {EmployeeId}", entity.RequestNumber, entity.EmployeeId);
        return entity.ToDto();
    }

    public async Task<StaffOvertimeRequestDto> UpdateAsync(UpdateStaffOvertimeRequestDto dto, Guid userId, CancellationToken ct = default)
    {
        var entity = await _repository.GetByIdAsync(dto.Id);
        if (entity == null)
            throw new ArgumentException($"Overtime request '{dto.Id}' not found.");

        if (entity.Status != OvertimeRequestStatus.Pending)
            throw new InvalidOperationException("Only pending overtime requests can be edited.");

        entity.UpdateEntity(dto, userId);
        await _repository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(ct);
        return entity.ToDto();
    }

    public async Task<StaffOvertimeRequestDto> ApproveAsync(Guid requestId, string? comments, Guid userId, CancellationToken ct = default)
    {
        var entity = await _repository.GetByIdAsync(requestId);
        if (entity == null)
            throw new ArgumentException($"Overtime request '{requestId}' not found.");

        if (entity.Status != OvertimeRequestStatus.Pending)
            throw new InvalidOperationException("Only pending overtime requests can be approved.");

        entity.Status = OvertimeRequestStatus.Approved;
        entity.ApprovedById = userId;
        entity.ApprovalDate = DateTime.UtcNow;
        entity.ApprovalComments = comments;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();

        await _repository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(ct);

        _logger.LogInformation("Overtime request {Number} approved by {UserId}", entity.RequestNumber, userId);
        return entity.ToDto();
    }

    public async Task<StaffOvertimeRequestDto> RejectAsync(Guid requestId, string rejectionReason, Guid userId, CancellationToken ct = default)
    {
        var entity = await _repository.GetByIdAsync(requestId);
        if (entity == null)
            throw new ArgumentException($"Overtime request '{requestId}' not found.");

        if (entity.Status != OvertimeRequestStatus.Pending)
            throw new InvalidOperationException("Only pending overtime requests can be rejected.");

        entity.Status = OvertimeRequestStatus.Rejected;
        entity.RejectedDate = DateTime.UtcNow;
        entity.RejectionReason = rejectionReason;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();

        await _repository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(ct);

        _logger.LogInformation("Overtime request {Number} rejected by {UserId}", entity.RequestNumber, userId);
        return entity.ToDto();
    }

    public async Task<StaffOvertimeRequestDto> ConfirmActualHoursAsync(Guid requestId, decimal actualHours, string? supervisorNotes, Guid userId, CancellationToken ct = default)
    {
        var entity = await _repository.GetByIdAsync(requestId);
        if (entity == null)
            throw new ArgumentException($"Overtime request '{requestId}' not found.");

        if (entity.Status != OvertimeRequestStatus.Approved)
            throw new InvalidOperationException("Only approved requests can have actual hours confirmed.");

        entity.ActualOvertimeHours = actualHours;
        entity.SupervisorNotes = supervisorNotes;
        entity.SupervisorConfirmedById = userId;
        entity.SupervisorConfirmedDate = DateTime.UtcNow;
        entity.Status = OvertimeRequestStatus.Completed;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();

        await _repository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(ct);

        _logger.LogInformation("Actual hours confirmed for overtime request {Number}: {Hours}h", entity.RequestNumber, actualHours);
        return entity.ToDto();
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await _repository.GetByIdAsync(id);
        if (entity == null)
            throw new ArgumentException($"Overtime request '{id}' not found.");

        if (entity.Status != OvertimeRequestStatus.Pending)
            throw new InvalidOperationException("Only pending overtime requests can be deleted.");

        await _repository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(ct);
        return true;
    }

    private async Task<string> GenerateRequestNumberAsync(CancellationToken ct)
    {
        var count = await _repository.GetQueryable().CountAsync(ct);
        return $"OT-{DateTime.UtcNow:yyyyMMdd}-{(count + 1):D5}";
    }
}

#endregion

// ============================================================================
// EMPLOYEE BIOMETRIC SERVICE
// ============================================================================

#region Employee Biometric Service

public class EmployeeBiometricService : IEmployeeBiometricService
{
    private readonly IEmployeeBiometricRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<EmployeeBiometricService> _logger;

    public EmployeeBiometricService(
        IEmployeeBiometricRepository repository,
        IUnitOfWork unitOfWork,
        ILogger<EmployeeBiometricService> logger)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<EmployeeBiometricDto> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await _repository.GetByIdAsync(id);
        if (entity == null)
            throw new ArgumentException($"Biometric record '{id}' not found.");
        return entity.ToDto();
    }

    public async Task<IEnumerable<EmployeeBiometricSummaryDto>> GetByEmployeeIdAsync(Guid employeeId, CancellationToken ct = default)
    {
        var entities = await _repository.GetByEmployeeIdAsync(employeeId);
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<EmployeeBiometricSummaryDto>> GetActiveBiometricsForEmployeeAsync(Guid employeeId, CancellationToken ct = default)
    {
        var entities = await _repository.GetActiveBiometricsForEmployeeAsync(employeeId);
        return entities.ToSummaryDtoList();
    }

    public async Task<EmployeeBiometricDto?> GetByEmployeeAndTypeAsync(Guid employeeId, BiometricType type, CancellationToken ct = default)
    {
        var entity = await _repository.GetByEmployeeAndTypeAsync(employeeId, type);
        return entity?.ToDto();
    }

    public async Task<IEnumerable<EmployeeBiometricSummaryDto>> GetByTypeAsync(BiometricType type, CancellationToken ct = default)
    {
        var entities = await _repository.GetByTypeAsync(type);
        return entities.ToSummaryDtoList();
    }

    public async Task<PagedResult<EmployeeBiometricSummaryDto>> GetPagedAsync(int pageNumber, int pageSize, CancellationToken ct = default)
    {
        var query = _repository.GetQueryable();
        var totalCount = await query.CountAsync(ct);
        var items = await query
            .OrderBy(b => b.EmployeeId)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new PagedResult<EmployeeBiometricSummaryDto>
        {
            Items = items.ToSummaryDtoList(),
            TotalCount = totalCount,
            Page = pageNumber,
            PageSize = pageSize
        };
    }

    public async Task<EmployeeBiometricDto> EnrolAsync(EnrollBiometricDto dto, Guid tenantId, Guid userId, CancellationToken ct = default)
    {
        var entity = dto.ToEntity(tenantId, userId);
        await _repository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(ct);

        _logger.LogInformation("Biometric enrolled for employee {EmployeeId}, type {Type}", dto.EmployeeId, dto.BiometricType);
        return entity.ToDto();
    }

    public async Task<EmployeeBiometricDto> UpdateAsync(UpdateEmployeeBiometricDto dto, Guid userId, CancellationToken ct = default)
    {
        var entity = await _repository.GetByIdAsync(dto.Id);
        if (entity == null)
            throw new ArgumentException($"Biometric record '{dto.Id}' not found.");

        entity.UpdateEntity(dto, userId);
        await _repository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(ct);
        return entity.ToDto();
    }

    public async Task<bool> RevokeAsync(Guid id, string reason, Guid userId, CancellationToken ct = default)
    {
        var entity = await _repository.GetByIdAsync(id);
        if (entity == null)
            throw new ArgumentException($"Biometric record '{id}' not found.");

        entity.IsActive = false;
        entity.RevokedReason = reason;
        entity.RevokedDate = DateTime.UtcNow;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();

        await _repository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(ct);

        _logger.LogInformation("Biometric {Id} revoked: {Reason}", id, reason);
        return true;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await _repository.GetByIdAsync(id);
        if (entity == null)
            throw new ArgumentException($"Biometric record '{id}' not found.");

        await _repository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(ct);
        return true;
    }
}

#endregion

// ============================================================================
// STAFF ATTENDANCE DEVICE SERVICE
// ============================================================================

#region Staff Attendance Device Service

public class StaffAttendanceDeviceService : IStaffAttendanceDeviceService
{
    private readonly IStaffAttendanceDeviceRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<StaffAttendanceDeviceService> _logger;

    public StaffAttendanceDeviceService(
        IStaffAttendanceDeviceRepository repository,
        IUnitOfWork unitOfWork,
        ILogger<StaffAttendanceDeviceService> logger)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<StaffAttendanceDeviceDto> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await _repository.GetByIdAsync(id);
        if (entity == null)
            throw new ArgumentException($"Attendance device '{id}' not found.");
        return entity.ToDto();
    }

    public async Task<StaffAttendanceDeviceDto?> GetByExternalDeviceIdAsync(string externalDeviceId, CancellationToken ct = default)
    {
        var entity = await _repository.GetByExternalDeviceIdAsync(externalDeviceId);
        return entity?.ToDto();
    }

    public async Task<IEnumerable<StaffAttendanceDeviceSummaryDto>> GetAllAsync(CancellationToken ct = default)
    {
        var entities = await _repository.GetAllAsync();
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<StaffAttendanceDeviceSummaryDto>> GetActiveDevicesAsync(CancellationToken ct = default)
    {
        var entities = await _repository.GetActiveDevicesAsync();
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<StaffAttendanceDeviceSummaryDto>> GetByLocationIdAsync(Guid locationId, CancellationToken ct = default)
    {
        var entities = await _repository.GetByLocationIdAsync(locationId);
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<StaffAttendanceDeviceSummaryDto>> GetDevicesOverdueForSyncAsync(int hoursThreshold = 24, CancellationToken ct = default)
    {
        var entities = await _repository.GetDevicesOverdueForSyncAsync(hoursThreshold);
        return entities.ToSummaryDtoList();
    }

    public async Task<PagedResult<StaffAttendanceDeviceSummaryDto>> GetPagedAsync(int pageNumber, int pageSize, CancellationToken ct = default)
    {
        var query = _repository.GetQueryable();
        var totalCount = await query.CountAsync(ct);
        var items = await query
            .OrderBy(d => d.DeviceName)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new PagedResult<StaffAttendanceDeviceSummaryDto>
        {
            Items = items.ToSummaryDtoList(),
            TotalCount = totalCount,
            Page = pageNumber,
            PageSize = pageSize
        };
    }

    public async Task<StaffAttendanceDeviceDto> RegisterAsync(CreateStaffAttendanceDeviceDto dto, Guid tenantId, Guid userId, CancellationToken ct = default)
    {
        var existing = await _repository.GetByExternalDeviceIdAsync(dto.DeviceId);
        if (existing != null)
            throw new InvalidOperationException($"A device with ID '{dto.DeviceId}' is already registered.");

        var entity = dto.ToEntity(tenantId, userId);
        await _repository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(ct);

        _logger.LogInformation("Attendance device registered: {Name} ({DeviceId})", entity.DeviceName, entity.DeviceId);
        return entity.ToDto();
    }

    public async Task<StaffAttendanceDeviceDto> UpdateAsync(UpdateStaffAttendanceDeviceDto dto, Guid userId, CancellationToken ct = default)
    {
        var entity = await _repository.GetByIdAsync(dto.Id);
        if (entity == null)
            throw new ArgumentException($"Attendance device '{dto.Id}' not found.");

        entity.UpdateEntity(dto, userId);
        await _repository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(ct);
        return entity.ToDto();
    }

    public async Task<StaffAttendanceDeviceDto> RecordSyncAsync(Guid deviceId, int? pendingSyncCount, Guid userId, CancellationToken ct = default)
    {
        var entity = await _repository.GetByIdAsync(deviceId);
        if (entity == null)
            throw new ArgumentException($"Attendance device '{deviceId}' not found.");

        entity.LastSyncDate = DateTime.UtcNow;
        entity.PendingSyncCount = pendingSyncCount ?? 0;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();

        await _repository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(ct);

        _logger.LogInformation("Sync recorded for device {DeviceId}: {Count} pending", deviceId, pendingSyncCount ?? 0);
        return entity.ToDto();
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await _repository.GetByIdAsync(id);
        if (entity == null)
            throw new ArgumentException($"Attendance device '{id}' not found.");

        await _repository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(ct);
        return true;
    }
}

#endregion
