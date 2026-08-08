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
// WORK SCHEDULE SERVICE
// ============================================================================

#region Work Schedule Service

public class WorkScheduleService : IWorkScheduleService
{
    private readonly IWorkScheduleRepository _repository;
    private readonly IShiftDefinitionRepository _shiftRepository;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<WorkScheduleService> _logger;

    public WorkScheduleService(
        IWorkScheduleRepository repository,
        IShiftDefinitionRepository shiftRepository,
        ICurrentUserProvider currentUserProvider,
        IUnitOfWork unitOfWork,
        ILogger<WorkScheduleService> logger)
    {
        _repository = repository;
        _shiftRepository = shiftRepository;
        _currentUserProvider = currentUserProvider;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    // The ApplicationDbContext is registered without a tenant, so its global tenant query-filter and
    // TenantId auto-stamp are inert. Following the RHEMA convention, this service scopes every read and
    // mutation to the authenticated tenant explicitly.
    private Guid GetTenantId()
    {
        var tenantId = _currentUserProvider.TenantId;
        if (tenantId == Guid.Empty)
            throw new InvalidOperationException("No tenant is associated with the current user.");
        return tenantId;
    }

    private Guid RequireCurrentTenant(Guid tenantId)
    {
        var current = GetTenantId();
        if (tenantId != Guid.Empty && tenantId != current)
            throw new UnauthorizedAccessException("The supplied tenant does not match the authenticated tenant.");
        return current;
    }

    private async Task<WorkSchedule> GetOwnedScheduleAsync(Guid id)
    {
        var entity = await _repository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Work schedule '{id}' not found.");
        return entity;
    }

    private async Task<ShiftDefinition> GetOwnedShiftAsync(Guid id)
    {
        var entity = await _shiftRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Shift definition '{id}' not found.");
        return entity;
    }

    public async Task<WorkScheduleDto> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        var entity = await _repository.GetWithShiftsAsync(id);
        if (entity == null || entity.TenantId != tenantId)
            throw new ArgumentException($"Work schedule '{id}' not found.");
        return entity.ToDto();
    }

    public async Task<WorkScheduleDto?> GetDefaultScheduleAsync(CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        var entity = await _repository.GetDefaultScheduleAsync();
        return entity?.TenantId == tenantId ? entity.ToDto() : null;
    }

    public async Task<IEnumerable<WorkScheduleSummaryDto>> GetAllAsync(CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        var entities = (await _repository.GetAllAsync()).Where(e => e.TenantId == tenantId);
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<WorkScheduleSummaryDto>> GetActiveSchedulesAsync(CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        var entities = (await _repository.GetActiveSchedulesAsync()).Where(e => e.TenantId == tenantId);
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<WorkScheduleSummaryDto>> GetByTypeAsync(WorkScheduleType type, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        var entities = (await _repository.GetByTypeAsync(type)).Where(e => e.TenantId == tenantId);
        return entities.ToSummaryDtoList();
    }

    public async Task<PagedResult<WorkScheduleSummaryDto>> GetPagedAsync(int pageNumber, int pageSize, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        var query = _repository.GetQueryable().Where(s => s.TenantId == tenantId);
        var totalCount = await query.CountAsync(ct);
        var items = await query
            .OrderBy(s => s.ScheduleName)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new PagedResult<WorkScheduleSummaryDto>
        {
            Items = items.ToSummaryDtoList(),
            TotalCount = totalCount,
            Page = pageNumber,
            PageSize = pageSize
        };
    }

    public async Task<WorkScheduleDto> CreateAsync(CreateWorkScheduleDto dto, Guid tenantId, Guid userId, CancellationToken ct = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        var entity = dto.ToEntity(tenantId, userId);
        await _repository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(ct);

        _logger.LogInformation("Work schedule created: {Name}", entity.ScheduleName);
        return entity.ToDto();
    }

    public async Task<WorkScheduleDto> UpdateAsync(UpdateWorkScheduleDto dto, Guid userId, CancellationToken ct = default)
    {
        var entity = await GetOwnedScheduleAsync(dto.Id);

        entity.UpdateEntity(dto, userId);
        await _repository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(ct);

        return entity.ToDto();
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await GetOwnedScheduleAsync(id);

        await _repository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(ct);
        return true;
    }

    public async Task<ShiftDefinitionDto> AddShiftAsync(CreateShiftDefinitionDto dto, Guid tenantId, Guid userId, CancellationToken ct = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        await GetOwnedScheduleAsync(dto.WorkScheduleId);

        var entity = dto.ToEntity(tenantId, userId);
        await _shiftRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(ct);
        return entity.ToDto();
    }

    public async Task<IEnumerable<ShiftDefinitionSummaryDto>> GetShiftsAsync(Guid workScheduleId, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        await GetOwnedScheduleAsync(workScheduleId);
        var entities = (await _shiftRepository.GetByWorkScheduleIdAsync(workScheduleId))
            .Where(e => e.TenantId == tenantId);
        return entities.ToSummaryDtoList();
    }

    public async Task<ShiftDefinitionDto> UpdateShiftAsync(UpdateShiftDefinitionDto dto, Guid userId, CancellationToken ct = default)
    {
        var entity = await GetOwnedShiftAsync(dto.Id);

        entity.UpdateEntity(dto, userId);
        await _shiftRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(ct);
        return entity.ToDto();
    }

    public async Task<bool> DeleteShiftAsync(Guid shiftId, CancellationToken ct = default)
    {
        var entity = await GetOwnedShiftAsync(shiftId);

        await _shiftRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(ct);
        return true;
    }
}

#endregion

// ============================================================================
// EMPLOYEE WORK SCHEDULE SERVICE
// ============================================================================

#region Employee Work Schedule Service

public class EmployeeWorkScheduleService : IEmployeeWorkScheduleService
{
    private readonly IEmployeeWorkScheduleRepository _repository;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<EmployeeWorkScheduleService> _logger;

    public EmployeeWorkScheduleService(
        IEmployeeWorkScheduleRepository repository,
        ICurrentUserProvider currentUserProvider,
        IUnitOfWork unitOfWork,
        ILogger<EmployeeWorkScheduleService> logger)
    {
        _repository = repository;
        _currentUserProvider = currentUserProvider;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    // The ApplicationDbContext is registered without a tenant, so its global tenant query-filter and
    // TenantId auto-stamp are inert. Following the RHEMA convention, this service scopes every read and
    // mutation to the authenticated tenant explicitly.
    private Guid GetTenantId()
    {
        var tenantId = _currentUserProvider.TenantId;
        if (tenantId == Guid.Empty)
            throw new InvalidOperationException("No tenant is associated with the current user.");
        return tenantId;
    }

    private Guid RequireCurrentTenant(Guid tenantId)
    {
        var current = GetTenantId();
        if (tenantId != Guid.Empty && tenantId != current)
            throw new UnauthorizedAccessException("The supplied tenant does not match the authenticated tenant.");
        return current;
    }

    private async Task<EmployeeWorkSchedule> GetOwnedAsync(Guid id)
    {
        var entity = await _repository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Employee work schedule assignment '{id}' not found.");
        return entity;
    }

    public async Task<EmployeeWorkScheduleDto> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await GetOwnedAsync(id);
        return entity.ToDto();
    }

    public async Task<EmployeeWorkScheduleDto?> GetCurrentForEmployeeAsync(Guid employeeId, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        var entity = await _repository.GetCurrentForEmployeeAsync(employeeId);
        return entity?.TenantId == tenantId ? entity.ToDto() : null;
    }

    public async Task<IEnumerable<EmployeeWorkScheduleDto>> GetAllForEmployeeAsync(Guid employeeId, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        var entities = (await _repository.GetAllForEmployeeAsync(employeeId))
            .Where(e => e.TenantId == tenantId);
        return entities.Select(e => e.ToDto()).ToList();
    }

    public async Task<IEnumerable<EmployeeWorkScheduleDto>> GetByWorkScheduleIdAsync(Guid workScheduleId, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        var entities = (await _repository.GetByWorkScheduleIdAsync(workScheduleId))
            .Where(e => e.TenantId == tenantId);
        return entities.Select(e => e.ToDto()).ToList();
    }

    public async Task<PagedResult<EmployeeWorkScheduleDto>> GetPagedAsync(int pageNumber, int pageSize, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        var query = _repository.GetQueryable()
            .Include(e => e.Employee)
            .Include(e => e.WorkSchedule)
            .Include(e => e.AssignedBy)
            .Where(e => e.TenantId == tenantId);
        var totalCount = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(e => e.EffectiveDate)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new PagedResult<EmployeeWorkScheduleDto>
        {
            Items = items.Select(e => e.ToDto()).ToList(),
            TotalCount = totalCount,
            Page = pageNumber,
            PageSize = pageSize
        };
    }

    public async Task<EmployeeWorkScheduleDto> AssignAsync(AssignEmployeeWorkScheduleDto dto, Guid tenantId, Guid userId, CancellationToken ct = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        var entity = dto.ToEntity(tenantId, userId);
        await _repository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(ct);

        _logger.LogInformation("Work schedule assigned to employee {EmployeeId}", dto.EmployeeId);
        return entity.ToDto();
    }

    public async Task<EmployeeWorkScheduleDto> UpdateAsync(UpdateEmployeeWorkScheduleDto dto, Guid userId, CancellationToken ct = default)
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
// SHIFT DEFINITION SERVICE
// ============================================================================

#region Shift Definition Service

public class ShiftDefinitionService : IShiftDefinitionService
{
    private readonly IShiftDefinitionRepository _repository;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<ShiftDefinitionService> _logger;

    public ShiftDefinitionService(
        IShiftDefinitionRepository repository,
        ICurrentUserProvider currentUserProvider,
        IUnitOfWork unitOfWork,
        ILogger<ShiftDefinitionService> logger)
    {
        _repository = repository;
        _currentUserProvider = currentUserProvider;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    // The ApplicationDbContext is registered without a tenant, so its global tenant query-filter and
    // TenantId auto-stamp are inert. Following the RHEMA convention, this service scopes every read and
    // mutation to the authenticated tenant explicitly.
    private Guid GetTenantId()
    {
        var tenantId = _currentUserProvider.TenantId;
        if (tenantId == Guid.Empty)
            throw new InvalidOperationException("No tenant is associated with the current user.");
        return tenantId;
    }

    private Guid RequireCurrentTenant(Guid tenantId)
    {
        var current = GetTenantId();
        if (tenantId != Guid.Empty && tenantId != current)
            throw new UnauthorizedAccessException("The supplied tenant does not match the authenticated tenant.");
        return current;
    }

    private async Task<ShiftDefinition> GetOwnedAsync(Guid id)
    {
        var entity = await _repository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Shift definition '{id}' not found.");
        return entity;
    }

    public async Task<ShiftDefinitionDto> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await GetOwnedAsync(id);
        return entity.ToDto();
    }

    public async Task<IEnumerable<ShiftDefinitionSummaryDto>> GetByWorkScheduleIdAsync(Guid workScheduleId, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        var entities = (await _repository.GetByWorkScheduleIdAsync(workScheduleId))
            .Where(e => e.TenantId == tenantId);
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<ShiftDefinitionSummaryDto>> GetActiveShiftsAsync(CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        var entities = (await _repository.GetActiveShiftsAsync()).Where(e => e.TenantId == tenantId);
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<ShiftDefinitionSummaryDto>> GetNightShiftsAsync(CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        var entities = (await _repository.GetNightShiftsAsync()).Where(e => e.TenantId == tenantId);
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<ShiftDefinitionSummaryDto>> GetByTypeAsync(ShiftType type, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        var entities = (await _repository.GetByTypeAsync(type)).Where(e => e.TenantId == tenantId);
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<ShiftDefinitionSummaryDto>> GetAllAsync(CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        var entities = (await _repository.GetAllAsync()).Where(e => e.TenantId == tenantId);
        return entities.ToSummaryDtoList();
    }

    public async Task<ShiftDefinitionDto> CreateAsync(CreateShiftDefinitionDto dto, Guid tenantId, Guid userId, CancellationToken ct = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        var entity = dto.ToEntity(tenantId, userId);
        await _repository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(ct);

        _logger.LogInformation("Shift definition created: {Name}", entity.ShiftName);
        return entity.ToDto();
    }

    public async Task<ShiftDefinitionDto> UpdateAsync(UpdateShiftDefinitionDto dto, Guid userId, CancellationToken ct = default)
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
// SHIFT ASSIGNMENT SERVICE
// ============================================================================

#region Shift Assignment Service

public class ShiftAssignmentService : IShiftAssignmentService
{
    private readonly IShiftAssignmentRepository _repository;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<ShiftAssignmentService> _logger;

    public ShiftAssignmentService(
        IShiftAssignmentRepository repository,
        ICurrentUserProvider currentUserProvider,
        IUnitOfWork unitOfWork,
        ILogger<ShiftAssignmentService> logger)
    {
        _repository = repository;
        _currentUserProvider = currentUserProvider;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    // The ApplicationDbContext is registered without a tenant, so its global tenant query-filter and
    // TenantId auto-stamp are inert. Following the RHEMA convention, this service scopes every read and
    // mutation to the authenticated tenant explicitly.
    private Guid GetTenantId()
    {
        var tenantId = _currentUserProvider.TenantId;
        if (tenantId == Guid.Empty)
            throw new InvalidOperationException("No tenant is associated with the current user.");
        return tenantId;
    }

    private Guid RequireCurrentTenant(Guid tenantId)
    {
        var current = GetTenantId();
        if (tenantId != Guid.Empty && tenantId != current)
            throw new UnauthorizedAccessException("The supplied tenant does not match the authenticated tenant.");
        return current;
    }

    private async Task<ShiftAssignment> GetOwnedAsync(Guid id)
    {
        var entity = await _repository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Shift assignment '{id}' not found.");
        return entity;
    }

    public async Task<ShiftAssignmentDto> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await GetOwnedAsync(id);
        return entity.ToDto();
    }

    public async Task<ShiftAssignmentDto?> GetCurrentAssignmentForEmployeeAsync(Guid employeeId, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        var entity = await _repository.GetCurrentAssignmentForEmployeeAsync(employeeId);
        return entity?.TenantId == tenantId ? entity.ToDto() : null;
    }

    public async Task<IEnumerable<ShiftAssignmentDto>> GetByEmployeeIdAsync(Guid employeeId, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        var entities = (await _repository.GetByEmployeeIdAsync(employeeId))
            .Where(e => e.TenantId == tenantId);
        return entities.Select(e => e.ToDto()).ToList();
    }

    public async Task<IEnumerable<ShiftAssignmentDto>> GetByShiftDefinitionIdAsync(Guid shiftDefinitionId, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        var entities = (await _repository.GetByShiftDefinitionIdAsync(shiftDefinitionId))
            .Where(e => e.TenantId == tenantId);
        return entities.Select(e => e.ToDto()).ToList();
    }

    public async Task<IEnumerable<ShiftAssignmentDto>> GetActiveAssignmentsAsync(DateTime? asOf = null, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        var entities = (await _repository.GetActiveAssignmentsAsync(asOf))
            .Where(e => e.TenantId == tenantId);
        return entities.Select(e => e.ToDto()).ToList();
    }

    public async Task<ShiftAssignmentDto> AssignAsync(CreateShiftAssignmentDto dto, Guid tenantId, Guid userId, CancellationToken ct = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        var entity = dto.ToEntity(tenantId, userId);
        await _repository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(ct);

        _logger.LogInformation("Shift assigned to employee {EmployeeId}", dto.EmployeeId);
        return entity.ToDto();
    }

    public async Task<ShiftAssignmentDto> UpdateAsync(UpdateShiftAssignmentDto dto, Guid userId, CancellationToken ct = default)
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
// SHIFT ROTATION PLAN SERVICE
// ============================================================================

#region Shift Rotation Plan Service

public class ShiftRotationPlanService : IShiftRotationPlanService
{
    private readonly IShiftRotationPlanRepository _repository;
    private readonly IShiftRotationStageRepository _stageRepository;
    private readonly IShiftRotationMemberRepository _memberRepository;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<ShiftRotationPlanService> _logger;

    public ShiftRotationPlanService(
        IShiftRotationPlanRepository repository,
        IShiftRotationStageRepository stageRepository,
        IShiftRotationMemberRepository memberRepository,
        ICurrentUserProvider currentUserProvider,
        IUnitOfWork unitOfWork,
        ILogger<ShiftRotationPlanService> logger)
    {
        _repository = repository;
        _stageRepository = stageRepository;
        _memberRepository = memberRepository;
        _currentUserProvider = currentUserProvider;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    // The ApplicationDbContext is registered without a tenant, so its global tenant query-filter and
    // TenantId auto-stamp are inert. Following the RHEMA convention, this service scopes every read and
    // mutation to the authenticated tenant explicitly.
    private Guid GetTenantId()
    {
        var tenantId = _currentUserProvider.TenantId;
        if (tenantId == Guid.Empty)
            throw new InvalidOperationException("No tenant is associated with the current user.");
        return tenantId;
    }

    private Guid RequireCurrentTenant(Guid tenantId)
    {
        var current = GetTenantId();
        if (tenantId != Guid.Empty && tenantId != current)
            throw new UnauthorizedAccessException("The supplied tenant does not match the authenticated tenant.");
        return current;
    }

    private async Task<ShiftRotationPlan> GetOwnedPlanAsync(Guid id)
    {
        var entity = await _repository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Shift rotation plan '{id}' not found.");
        return entity;
    }

    private async Task<ShiftRotationStage> GetOwnedStageAsync(Guid id)
    {
        var entity = await _stageRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Rotation stage '{id}' not found.");
        return entity;
    }

    private async Task<ShiftRotationMember> GetOwnedMemberAsync(Guid id)
    {
        var entity = await _memberRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Rotation member '{id}' not found.");
        return entity;
    }

    public async Task<ShiftRotationPlanDto> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await GetOwnedPlanAsync(id);
        return entity.ToDto();
    }

    public async Task<IEnumerable<ShiftRotationPlanSummaryDto>> GetAllAsync(CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        var entities = (await _repository.GetAllAsync()).Where(e => e.TenantId == tenantId);
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<ShiftRotationPlanSummaryDto>> GetActivePlansAsync(CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        var entities = (await _repository.GetActivePlansAsync()).Where(e => e.TenantId == tenantId);
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<ShiftRotationPlanSummaryDto>> GetByRotationCycleAsync(ShiftRotationCycle cycle, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        var entities = (await _repository.GetByRotationCycleAsync(cycle)).Where(e => e.TenantId == tenantId);
        return entities.ToSummaryDtoList();
    }

    public async Task<ShiftRotationPlanDto> GetWithStagesAsync(Guid id, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        var entity = await _repository.GetWithStagesAsync(id);
        if (entity == null || entity.TenantId != tenantId)
            throw new ArgumentException($"Shift rotation plan '{id}' not found.");
        return entity.ToDto();
    }

    public async Task<ShiftRotationPlanDto> GetWithMembersAsync(Guid id, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        var entity = await _repository.GetWithMembersAsync(id);
        if (entity == null || entity.TenantId != tenantId)
            throw new ArgumentException($"Shift rotation plan '{id}' not found.");
        return entity.ToDto();
    }

    public async Task<PagedResult<ShiftRotationPlanSummaryDto>> GetPagedAsync(int pageNumber, int pageSize, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        var query = _repository.GetQueryable().Where(p => p.TenantId == tenantId);
        var totalCount = await query.CountAsync(ct);
        var items = await query
            .OrderBy(p => p.PlanName)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new PagedResult<ShiftRotationPlanSummaryDto>
        {
            Items = items.ToSummaryDtoList(),
            TotalCount = totalCount,
            Page = pageNumber,
            PageSize = pageSize
        };
    }

    public async Task<ShiftRotationPlanDto> CreateAsync(CreateShiftRotationPlanDto dto, Guid tenantId, Guid userId, CancellationToken ct = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        var entity = dto.ToEntity(tenantId, userId);
        await _repository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(ct);

        _logger.LogInformation("Shift rotation plan created: {Name}", entity.PlanName);
        return entity.ToDto();
    }

    public async Task<ShiftRotationPlanDto> UpdateAsync(UpdateShiftRotationPlanDto dto, Guid userId, CancellationToken ct = default)
    {
        var entity = await GetOwnedPlanAsync(dto.Id);

        entity.UpdateEntity(dto, userId);
        await _repository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(ct);
        return entity.ToDto();
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await GetOwnedPlanAsync(id);

        await _repository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(ct);
        return true;
    }

    public async Task<ShiftRotationStageDto> AddStageAsync(CreateShiftRotationStageDto dto, Guid tenantId, Guid userId, CancellationToken ct = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        await GetOwnedPlanAsync(dto.ShiftRotationPlanId);

        var entity = dto.ToEntity(tenantId, userId);
        await _stageRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(ct);
        return entity.ToDto();
    }

    public async Task<IEnumerable<ShiftRotationStageDto>> GetStagesAsync(Guid planId, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        await GetOwnedPlanAsync(planId);
        var entities = (await _stageRepository.GetByPlanIdAsync(planId))
            .Where(e => e.TenantId == tenantId);
        return entities.Select(e => e.ToDto()).ToList();
    }

    public async Task<ShiftRotationStageDto> UpdateStageAsync(UpdateShiftRotationStageDto dto, Guid userId, CancellationToken ct = default)
    {
        var entity = await GetOwnedStageAsync(dto.Id);

        entity.UpdateEntity(dto, userId);
        await _stageRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(ct);
        return entity.ToDto();
    }

    public async Task<bool> DeleteStageAsync(Guid stageId, CancellationToken ct = default)
    {
        var entity = await GetOwnedStageAsync(stageId);

        await _stageRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(ct);
        return true;
    }

    public async Task<ShiftRotationMemberDto> EnrollMemberAsync(AddShiftRotationMemberDto dto, Guid tenantId, Guid userId, CancellationToken ct = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        await GetOwnedPlanAsync(dto.ShiftRotationPlanId);

        var entity = dto.ToEntity(tenantId, userId);
        await _memberRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(ct);

        _logger.LogInformation("Employee {EmployeeId} enrolled in rotation plan {PlanId}", dto.EmployeeId, dto.ShiftRotationPlanId);
        return entity.ToDto();
    }

    public async Task<IEnumerable<ShiftRotationMemberDto>> GetMembersAsync(Guid planId, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        await GetOwnedPlanAsync(planId);
        var entities = (await _memberRepository.GetByPlanIdAsync(planId))
            .Where(e => e.TenantId == tenantId);
        return entities.Select(e => e.ToDto()).ToList();
    }

    public async Task<ShiftRotationMemberDto> UpdateMemberAsync(UpdateShiftRotationMemberDto dto, Guid userId, CancellationToken ct = default)
    {
        var entity = await GetOwnedMemberAsync(dto.Id);

        entity.UpdateEntity(dto, userId);
        await _memberRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(ct);
        return entity.ToDto();
    }

    public async Task<bool> RemoveMemberAsync(Guid memberId, Guid userId, CancellationToken ct = default)
    {
        var entity = await GetOwnedMemberAsync(memberId);

        await _memberRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(ct);
        return true;
    }
}

#endregion
