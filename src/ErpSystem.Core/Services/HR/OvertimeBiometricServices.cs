using ErpSystem.Application.HR.Extensions;
using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR.StaffAttendance;
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
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<PositionOvertimePolicyService> _logger;

    public PositionOvertimePolicyService(
        IPositionOvertimePolicyRepository repository,
        IEmployeeOvertimeOverrideRepository overrideRepository,
        ICurrentUserProvider currentUserProvider,
        IUnitOfWork unitOfWork,
        ILogger<PositionOvertimePolicyService> logger)
    {
        _repository = repository;
        _overrideRepository = overrideRepository;
        _currentUserProvider = currentUserProvider;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    // The ApplicationDbContext is registered without a tenant, so its global tenant query-filter and
    // TenantId auto-stamp are inert. Following the RHEMA convention, this service scopes reads/writes to
    // the current tenant explicitly.
    private Guid GetTenantId()
    {
        var tenantId = _currentUserProvider.TenantId;
        if (tenantId == Guid.Empty)
            throw new InvalidOperationException("No tenant is associated with the current user.");
        return tenantId;
    }

    // An overtime policy owned by another tenant is reported as missing rather than forbidden, so the endpoints do
    // not confirm that the id exists elsewhere.
    private async Task<PositionOvertimePolicy> GetOwnedPolicyAsync(Guid id)
    {
        var entity = await _repository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Overtime policy '{id}' not found.");
        return entity;
    }

    private async Task<EmployeeOvertimeOverride> GetOwnedOverrideAsync(Guid id)
    {
        var entity = await _overrideRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Overtime override '{id}' not found.");
        return entity;
    }

    public async Task<PositionOvertimePolicyDto> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await GetOwnedPolicyAsync(id);
        return entity.ToDto();
    }

    public async Task<IEnumerable<PositionOvertimePolicyDto>> GetByPositionIdAsync(Guid positionId, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        var entities = (await _repository.GetByPositionIdAsync(positionId)).Where(e => e.TenantId == tenantId);
        return entities.Select(e => e.ToDto()).ToList();
    }

    public async Task<IEnumerable<PositionOvertimePolicyDto>> GetByAllowanceTypeAsync(OvertimeAllowanceType allowanceType, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        var entities = (await _repository.GetByAllowanceTypeAsync(allowanceType)).Where(e => e.TenantId == tenantId);
        return entities.Select(e => e.ToDto()).ToList();
    }

    public async Task<PositionOvertimePolicyDto?> GetActiveForPositionAsync(Guid positionId, OvertimeAllowanceType allowanceType, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        var entity = await _repository.GetActiveForPositionAsync(positionId, allowanceType);
        return entity?.TenantId == tenantId ? entity.ToDto() : null;
    }

    public async Task<PositionOvertimePolicyDto> CreateAsync(CreatePositionOvertimePolicyDto dto, Guid tenantId, Guid userId, CancellationToken ct = default)
    {
        var current = GetTenantId();
        if (tenantId != Guid.Empty && tenantId != current)
            throw new UnauthorizedAccessException("The supplied tenant does not match the authenticated tenant.");

        var entity = dto.ToEntity(current, userId);
        await _repository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(ct);

        _logger.LogInformation("Overtime policy created for position {PositionId}", dto.PositionId);
        return entity.ToDto();
    }

    public async Task<PositionOvertimePolicyDto> UpdateAsync(UpdatePositionOvertimePolicyDto dto, Guid userId, CancellationToken ct = default)
    {
        var entity = await GetOwnedPolicyAsync(dto.Id);

        entity.UpdateEntity(dto, userId);
        await _repository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(ct);
        return entity.ToDto();
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await GetOwnedPolicyAsync(id);

        await _repository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(ct);
        return true;
    }

    public async Task<EmployeeOvertimeOverrideDto> AddOverrideAsync(CreateEmployeeOvertimeOverrideDto dto, Guid tenantId, Guid userId, CancellationToken ct = default)
    {
        var current = GetTenantId();
        if (tenantId != Guid.Empty && tenantId != current)
            throw new UnauthorizedAccessException("The supplied tenant does not match the authenticated tenant.");

        if (dto.PolicyId.HasValue)
            await GetOwnedPolicyAsync(dto.PolicyId.Value);

        var entity = dto.ToEntity(current, userId);
        await _overrideRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(ct);
        return entity.ToDto();
    }

    public async Task<IEnumerable<EmployeeOvertimeOverrideDto>> GetOverridesAsync(Guid policyId, CancellationToken ct = default)
    {
        await GetOwnedPolicyAsync(policyId);
        var tenantId = GetTenantId();
        var entities = (await _overrideRepository.GetByPolicyIdAsync(policyId)).Where(e => e.TenantId == tenantId);
        return entities.Select(e => e.ToDto()).ToList();
    }

    public async Task<EmployeeOvertimeOverrideDto> UpdateOverrideAsync(UpdateEmployeeOvertimeOverrideDto dto, Guid userId, CancellationToken ct = default)
    {
        var entity = await GetOwnedOverrideAsync(dto.Id);

        entity.UpdateEntity(dto, userId);
        await _overrideRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(ct);
        return entity.ToDto();
    }

    public async Task<bool> DeleteOverrideAsync(Guid overrideId, CancellationToken ct = default)
    {
        var entity = await GetOwnedOverrideAsync(overrideId);

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
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<EmployeeOvertimeOverrideService> _logger;

    public EmployeeOvertimeOverrideService(
        IEmployeeOvertimeOverrideRepository repository,
        ICurrentUserProvider currentUserProvider,
        IUnitOfWork unitOfWork,
        ILogger<EmployeeOvertimeOverrideService> logger)
    {
        _repository = repository;
        _currentUserProvider = currentUserProvider;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    // The ApplicationDbContext is registered without a tenant, so its global tenant query-filter and
    // TenantId auto-stamp are inert. Following the RHEMA convention, this service scopes reads/writes to
    // the current tenant explicitly.
    private Guid GetTenantId()
    {
        var tenantId = _currentUserProvider.TenantId;
        if (tenantId == Guid.Empty)
            throw new InvalidOperationException("No tenant is associated with the current user.");
        return tenantId;
    }

    // An overtime override owned by another tenant is reported as missing rather than forbidden, so the endpoints do
    // not confirm that the id exists elsewhere.
    private async Task<EmployeeOvertimeOverride> GetOwnedAsync(Guid id)
    {
        var entity = await _repository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Overtime override '{id}' not found.");
        return entity;
    }

    public async Task<EmployeeOvertimeOverrideDto> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await GetOwnedAsync(id);
        return entity.ToDto();
    }

    public async Task<IEnumerable<EmployeeOvertimeOverrideDto>> GetByEmployeeIdAsync(Guid employeeId, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        var entities = (await _repository.GetByEmployeeIdAsync(employeeId)).Where(e => e.TenantId == tenantId);
        return entities.Select(e => e.ToDto()).ToList();
    }

    public async Task<IEnumerable<EmployeeOvertimeOverrideDto>> GetActiveOverridesForEmployeeAsync(Guid employeeId, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        var entities = (await _repository.GetActiveOverridesForEmployeeAsync(employeeId)).Where(e => e.TenantId == tenantId);
        return entities.Select(e => e.ToDto()).ToList();
    }

    public async Task<EmployeeOvertimeOverrideDto> CreateAsync(CreateEmployeeOvertimeOverrideDto dto, Guid tenantId, Guid userId, CancellationToken ct = default)
    {
        var current = GetTenantId();
        if (tenantId != Guid.Empty && tenantId != current)
            throw new UnauthorizedAccessException("The supplied tenant does not match the authenticated tenant.");

        var entity = dto.ToEntity(current, userId);
        await _repository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(ct);

        _logger.LogInformation("Overtime override created for employee {EmployeeId}", dto.EmployeeId);
        return entity.ToDto();
    }

    public async Task<EmployeeOvertimeOverrideDto> UpdateAsync(UpdateEmployeeOvertimeOverrideDto dto, Guid userId, CancellationToken ct = default)
    {
        var entity = await GetOwnedAsync(dto.Id);

        entity.UpdateEntity(dto, userId);
        await _repository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(ct);
        return entity.ToDto();
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await GetOwnedAsync(id);

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
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<StaffOvertimeRequestService> _logger;

    public StaffOvertimeRequestService(
        IStaffOvertimeRequestRepository repository,
        ICurrentUserProvider currentUserProvider,
        IUnitOfWork unitOfWork,
        ILogger<StaffOvertimeRequestService> logger)
    {
        _repository = repository;
        _currentUserProvider = currentUserProvider;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    // The ApplicationDbContext is registered without a tenant, so its global tenant query-filter and
    // TenantId auto-stamp are inert. Following the RHEMA convention, this service scopes reads/writes to
    // the current tenant explicitly.
    private Guid GetTenantId()
    {
        var tenantId = _currentUserProvider.TenantId;
        if (tenantId == Guid.Empty)
            throw new InvalidOperationException("No tenant is associated with the current user.");
        return tenantId;
    }

    // An overtime request owned by another tenant is reported as missing rather than forbidden, so the endpoints do
    // not confirm that the id exists elsewhere.
    private async Task<StaffOvertimeRequest> GetOwnedAsync(Guid id)
    {
        var entity = await _repository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Overtime request '{id}' not found.");
        return entity;
    }

    private async Task<StaffOvertimeRequest> GetOwnedWithDetailsAsync(Guid id)
    {
        var entity = await _repository.GetWithFullDetailsAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Overtime request '{id}' not found.");
        return entity;
    }

    public async Task<StaffOvertimeRequestDto> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await GetOwnedWithDetailsAsync(id);
        return entity.ToDto();
    }

    public async Task<StaffOvertimeRequestDto?> GetByRequestNumberAsync(string requestNumber, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        var entity = await _repository.GetByRequestNumberAsync(requestNumber);
        return entity?.TenantId == tenantId ? entity.ToDto() : null;
    }

    public async Task<IEnumerable<StaffOvertimeRequestSummaryDto>> GetByEmployeeIdAsync(Guid employeeId, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        var entities = (await _repository.GetByEmployeeIdAsync(employeeId)).Where(e => e.TenantId == tenantId);
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<StaffOvertimeRequestSummaryDto>> GetByStatusAsync(OvertimeRequestStatus status, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        var entities = (await _repository.GetByStatusAsync(status)).Where(e => e.TenantId == tenantId);
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<StaffOvertimeRequestSummaryDto>> GetPendingApprovalAsync(CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        var entities = (await _repository.GetPendingApprovalAsync()).Where(e => e.TenantId == tenantId);
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<StaffOvertimeRequestSummaryDto>> GetPendingSupervisorConfirmationAsync(CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        var entities = (await _repository.GetPendingSupervisorConfirmationAsync()).Where(e => e.TenantId == tenantId);
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<StaffOvertimeRequestSummaryDto>> GetByDateRangeAsync(DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        var entities = (await _repository.GetByDateRangeAsync(from, to)).Where(e => e.TenantId == tenantId);
        return entities.ToSummaryDtoList();
    }

    public async Task<PagedResult<StaffOvertimeRequestSummaryDto>> GetPagedAsync(int pageNumber, int pageSize, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        var query = _repository.GetQueryable().Where(r => r.TenantId == tenantId);
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
        var current = GetTenantId();
        if (tenantId != Guid.Empty && tenantId != current)
            throw new UnauthorizedAccessException("The supplied tenant does not match the authenticated tenant.");

        var entity = dto.ToEntity(current, userId);
        entity.RequestNumber = await GenerateRequestNumberAsync(current, ct);
        entity.Status = OvertimeRequestStatus.Pending;
        await _repository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(ct);

        _logger.LogInformation("Overtime request {Number} created for employee {EmployeeId}", entity.RequestNumber, entity.EmployeeId);
        return entity.ToDto();
    }

    public async Task<StaffOvertimeRequestDto> UpdateAsync(UpdateStaffOvertimeRequestDto dto, Guid userId, CancellationToken ct = default)
    {
        var entity = await GetOwnedAsync(dto.Id);

        if (entity.Status != OvertimeRequestStatus.Pending)
            throw new InvalidOperationException("Only pending overtime requests can be edited.");

        entity.UpdateEntity(dto, userId);
        await _repository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(ct);
        return entity.ToDto();
    }

    public async Task<StaffOvertimeRequestDto> ApproveAsync(Guid requestId, string? comments, Guid userId, CancellationToken ct = default)
    {
        var entity = await GetOwnedAsync(requestId);

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
        var entity = await GetOwnedAsync(requestId);

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
        var entity = await GetOwnedAsync(requestId);

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
        var entity = await GetOwnedAsync(id);

        if (entity.Status != OvertimeRequestStatus.Pending)
            throw new InvalidOperationException("Only pending overtime requests can be deleted.");

        await _repository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(ct);
        return true;
    }

    private async Task<string> GenerateRequestNumberAsync(Guid tenantId, CancellationToken ct)
    {
        var count = await _repository.GetQueryable().CountAsync(r => r.TenantId == tenantId, ct);
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
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<EmployeeBiometricService> _logger;

    public EmployeeBiometricService(
        IEmployeeBiometricRepository repository,
        ICurrentUserProvider currentUserProvider,
        IUnitOfWork unitOfWork,
        ILogger<EmployeeBiometricService> logger)
    {
        _repository = repository;
        _currentUserProvider = currentUserProvider;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    // The ApplicationDbContext is registered without a tenant, so its global tenant query-filter and
    // TenantId auto-stamp are inert. Following the RHEMA convention, this service scopes reads/writes to
    // the current tenant explicitly.
    private Guid GetTenantId()
    {
        var tenantId = _currentUserProvider.TenantId;
        if (tenantId == Guid.Empty)
            throw new InvalidOperationException("No tenant is associated with the current user.");
        return tenantId;
    }

    // A biometric record owned by another tenant is reported as missing rather than forbidden, so the endpoints do
    // not confirm that the id exists elsewhere.
    private async Task<EmployeeBiometric> GetOwnedAsync(Guid id)
    {
        var entity = await _repository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Biometric record '{id}' not found.");
        return entity;
    }

    public async Task<EmployeeBiometricDto> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await GetOwnedAsync(id);
        return entity.ToDto();
    }

    public async Task<IEnumerable<EmployeeBiometricSummaryDto>> GetByEmployeeIdAsync(Guid employeeId, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        var entities = (await _repository.GetByEmployeeIdAsync(employeeId)).Where(e => e.TenantId == tenantId);
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<EmployeeBiometricSummaryDto>> GetActiveBiometricsForEmployeeAsync(Guid employeeId, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        var entities = (await _repository.GetActiveBiometricsForEmployeeAsync(employeeId)).Where(e => e.TenantId == tenantId);
        return entities.ToSummaryDtoList();
    }

    public async Task<EmployeeBiometricDto?> GetByEmployeeAndTypeAsync(Guid employeeId, BiometricType type, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        var entity = await _repository.GetByEmployeeAndTypeAsync(employeeId, type);
        return entity?.TenantId == tenantId ? entity.ToDto() : null;
    }

    public async Task<IEnumerable<EmployeeBiometricSummaryDto>> GetByTypeAsync(BiometricType type, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        var entities = (await _repository.GetByTypeAsync(type)).Where(e => e.TenantId == tenantId);
        return entities.ToSummaryDtoList();
    }

    public async Task<PagedResult<EmployeeBiometricSummaryDto>> GetPagedAsync(int pageNumber, int pageSize, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        var query = _repository.GetQueryable().Where(b => b.TenantId == tenantId);
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
        var current = GetTenantId();
        if (tenantId != Guid.Empty && tenantId != current)
            throw new UnauthorizedAccessException("The supplied tenant does not match the authenticated tenant.");

        var entity = dto.ToEntity(current, userId);
        await _repository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(ct);

        _logger.LogInformation("Biometric enrolled for employee {EmployeeId}, type {Type}", dto.EmployeeId, dto.BiometricType);
        return entity.ToDto();
    }

    public async Task<EmployeeBiometricDto> UpdateAsync(UpdateEmployeeBiometricDto dto, Guid userId, CancellationToken ct = default)
    {
        var entity = await GetOwnedAsync(dto.Id);

        entity.UpdateEntity(dto, userId);
        await _repository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(ct);
        return entity.ToDto();
    }

    public async Task<bool> RevokeAsync(Guid id, string reason, Guid userId, CancellationToken ct = default)
    {
        var entity = await GetOwnedAsync(id);

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
        var entity = await GetOwnedAsync(id);

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
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<StaffAttendanceDeviceService> _logger;

    public StaffAttendanceDeviceService(
        IStaffAttendanceDeviceRepository repository,
        ICurrentUserProvider currentUserProvider,
        IUnitOfWork unitOfWork,
        ILogger<StaffAttendanceDeviceService> logger)
    {
        _repository = repository;
        _currentUserProvider = currentUserProvider;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    // The ApplicationDbContext is registered without a tenant, so its global tenant query-filter and
    // TenantId auto-stamp are inert. Following the RHEMA convention, this service scopes reads/writes to
    // the current tenant explicitly.
    private Guid GetTenantId()
    {
        var tenantId = _currentUserProvider.TenantId;
        if (tenantId == Guid.Empty)
            throw new InvalidOperationException("No tenant is associated with the current user.");
        return tenantId;
    }

    // An attendance device owned by another tenant is reported as missing rather than forbidden, so the endpoints do
    // not confirm that the id exists elsewhere.
    private async Task<StaffAttendanceDevice> GetOwnedAsync(Guid id)
    {
        var entity = await _repository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Attendance device '{id}' not found.");
        return entity;
    }

    public async Task<StaffAttendanceDeviceDto> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await GetOwnedAsync(id);
        return entity.ToDto();
    }

    public async Task<StaffAttendanceDeviceDto?> GetByExternalDeviceIdAsync(string externalDeviceId, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        var entity = await _repository.GetByExternalDeviceIdAsync(externalDeviceId);
        return entity?.TenantId == tenantId ? entity.ToDto() : null;
    }

    public async Task<IEnumerable<StaffAttendanceDeviceSummaryDto>> GetAllAsync(CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        var entities = (await _repository.GetAllAsync()).Where(e => e.TenantId == tenantId);
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<StaffAttendanceDeviceSummaryDto>> GetActiveDevicesAsync(CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        var entities = (await _repository.GetActiveDevicesAsync()).Where(e => e.TenantId == tenantId);
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<StaffAttendanceDeviceSummaryDto>> GetByLocationIdAsync(Guid locationId, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        var entities = (await _repository.GetByLocationIdAsync(locationId)).Where(e => e.TenantId == tenantId);
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<StaffAttendanceDeviceSummaryDto>> GetDevicesOverdueForSyncAsync(int hoursThreshold = 24, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        var entities = (await _repository.GetDevicesOverdueForSyncAsync(hoursThreshold)).Where(e => e.TenantId == tenantId);
        return entities.ToSummaryDtoList();
    }

    public async Task<PagedResult<StaffAttendanceDeviceSummaryDto>> GetPagedAsync(int pageNumber, int pageSize, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        var query = _repository.GetQueryable().Where(d => d.TenantId == tenantId);
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
        var current = GetTenantId();
        if (tenantId != Guid.Empty && tenantId != current)
            throw new UnauthorizedAccessException("The supplied tenant does not match the authenticated tenant.");

        var existing = await _repository.GetByExternalDeviceIdAsync(dto.DeviceId);
        if (existing != null && existing.TenantId == current)
            throw new InvalidOperationException($"A device with ID '{dto.DeviceId}' is already registered.");

        var entity = dto.ToEntity(current, userId);
        await _repository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(ct);

        _logger.LogInformation("Attendance device registered: {Name} ({DeviceId})", entity.DeviceName, entity.DeviceId);
        return entity.ToDto();
    }

    public async Task<StaffAttendanceDeviceDto> UpdateAsync(UpdateStaffAttendanceDeviceDto dto, Guid userId, CancellationToken ct = default)
    {
        var entity = await GetOwnedAsync(dto.Id);

        entity.UpdateEntity(dto, userId);
        await _repository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(ct);
        return entity.ToDto();
    }

    public async Task<StaffAttendanceDeviceDto> RecordSyncAsync(Guid deviceId, int? pendingSyncCount, Guid userId, CancellationToken ct = default)
    {
        var entity = await GetOwnedAsync(deviceId);

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
        var entity = await GetOwnedAsync(id);

        await _repository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(ct);
        return true;
    }
}

#endregion
