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
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<WorkScheduleService> _logger;

    public WorkScheduleService(
        IWorkScheduleRepository repository,
        IShiftDefinitionRepository shiftRepository,
        IUnitOfWork unitOfWork,
        ILogger<WorkScheduleService> logger)
    {
        _repository = repository;
        _shiftRepository = shiftRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<WorkScheduleDto> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await _repository.GetWithShiftsAsync(id);
        if (entity == null)
            throw new ArgumentException($"Work schedule '{id}' not found.");
        return entity.ToDto();
    }

    public async Task<WorkScheduleDto?> GetDefaultScheduleAsync(CancellationToken ct = default)
    {
        var entity = await _repository.GetDefaultScheduleAsync();
        return entity?.ToDto();
    }

    public async Task<IEnumerable<WorkScheduleSummaryDto>> GetAllAsync(CancellationToken ct = default)
    {
        var entities = await _repository.GetAllAsync();
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<WorkScheduleSummaryDto>> GetActiveSchedulesAsync(CancellationToken ct = default)
    {
        var entities = await _repository.GetActiveSchedulesAsync();
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<WorkScheduleSummaryDto>> GetByTypeAsync(WorkScheduleType type, CancellationToken ct = default)
    {
        var entities = await _repository.GetByTypeAsync(type);
        return entities.ToSummaryDtoList();
    }

    public async Task<PagedResult<WorkScheduleSummaryDto>> GetPagedAsync(int pageNumber, int pageSize, CancellationToken ct = default)
    {
        var query = _repository.GetQueryable();
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
        var entity = dto.ToEntity(tenantId, userId);
        await _repository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(ct);

        _logger.LogInformation("Work schedule created: {Name}", entity.ScheduleName);
        return entity.ToDto();
    }

    public async Task<WorkScheduleDto> UpdateAsync(UpdateWorkScheduleDto dto, Guid userId, CancellationToken ct = default)
    {
        var entity = await _repository.GetByIdAsync(dto.Id);
        if (entity == null)
            throw new ArgumentException($"Work schedule '{dto.Id}' not found.");

        entity.UpdateEntity(dto, userId);
        await _repository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(ct);

        return entity.ToDto();
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await _repository.GetByIdAsync(id);
        if (entity == null)
            throw new ArgumentException($"Work schedule '{id}' not found.");

        await _repository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(ct);
        return true;
    }

    public async Task<ShiftDefinitionDto> AddShiftAsync(CreateShiftDefinitionDto dto, Guid tenantId, Guid userId, CancellationToken ct = default)
    {
        var entity = dto.ToEntity(tenantId, userId);
        await _shiftRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(ct);
        return entity.ToDto();
    }

    public async Task<IEnumerable<ShiftDefinitionSummaryDto>> GetShiftsAsync(Guid workScheduleId, CancellationToken ct = default)
    {
        var entities = await _shiftRepository.GetByWorkScheduleIdAsync(workScheduleId);
        return entities.ToSummaryDtoList();
    }

    public async Task<ShiftDefinitionDto> UpdateShiftAsync(UpdateShiftDefinitionDto dto, Guid userId, CancellationToken ct = default)
    {
        var entity = await _shiftRepository.GetByIdAsync(dto.Id);
        if (entity == null)
            throw new ArgumentException($"Shift definition '{dto.Id}' not found.");

        entity.UpdateEntity(dto, userId);
        await _shiftRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(ct);
        return entity.ToDto();
    }

    public async Task<bool> DeleteShiftAsync(Guid shiftId, CancellationToken ct = default)
    {
        var entity = await _shiftRepository.GetByIdAsync(shiftId);
        if (entity == null)
            throw new ArgumentException($"Shift definition '{shiftId}' not found.");

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
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<EmployeeWorkScheduleService> _logger;

    public EmployeeWorkScheduleService(
        IEmployeeWorkScheduleRepository repository,
        IUnitOfWork unitOfWork,
        ILogger<EmployeeWorkScheduleService> logger)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<EmployeeWorkScheduleDto> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await _repository.GetByIdAsync(id);
        if (entity == null)
            throw new ArgumentException($"Employee work schedule assignment '{id}' not found.");
        return entity.ToDto();
    }

    public async Task<EmployeeWorkScheduleDto?> GetCurrentForEmployeeAsync(Guid employeeId, CancellationToken ct = default)
    {
        var entity = await _repository.GetCurrentForEmployeeAsync(employeeId);
        return entity?.ToDto();
    }

    public async Task<IEnumerable<EmployeeWorkScheduleDto>> GetAllForEmployeeAsync(Guid employeeId, CancellationToken ct = default)
    {
        var entities = await _repository.GetAllForEmployeeAsync(employeeId);
        return entities.Select(e => e.ToDto()).ToList();
    }

    public async Task<IEnumerable<EmployeeWorkScheduleDto>> GetByWorkScheduleIdAsync(Guid workScheduleId, CancellationToken ct = default)
    {
        var entities = await _repository.GetByWorkScheduleIdAsync(workScheduleId);
        return entities.Select(e => e.ToDto()).ToList();
    }

    public async Task<PagedResult<EmployeeWorkScheduleDto>> GetPagedAsync(int pageNumber, int pageSize, CancellationToken ct = default)
    {
        var (items, totalCount) = await _repository.GetPagedWithCountAsync(pageNumber, pageSize);
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
        var entity = dto.ToEntity(tenantId, userId);
        await _repository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(ct);

        _logger.LogInformation("Work schedule assigned to employee {EmployeeId}", dto.EmployeeId);
        return entity.ToDto();
    }

    public async Task<EmployeeWorkScheduleDto> UpdateAsync(UpdateEmployeeWorkScheduleDto dto, Guid userId, CancellationToken ct = default)
    {
        var entity = await _repository.GetByIdAsync(dto.Id);
        if (entity == null)
            throw new ArgumentException($"Employee work schedule '{dto.Id}' not found.");

        entity.UpdateEntity(dto, userId);
        await _repository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(ct);
        return entity.ToDto();
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await _repository.GetByIdAsync(id);
        if (entity == null)
            throw new ArgumentException($"Employee work schedule '{id}' not found.");

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
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<ShiftDefinitionService> _logger;

    public ShiftDefinitionService(
        IShiftDefinitionRepository repository,
        IUnitOfWork unitOfWork,
        ILogger<ShiftDefinitionService> logger)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<ShiftDefinitionDto> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await _repository.GetByIdAsync(id);
        if (entity == null)
            throw new ArgumentException($"Shift definition '{id}' not found.");
        return entity.ToDto();
    }

    public async Task<IEnumerable<ShiftDefinitionSummaryDto>> GetByWorkScheduleIdAsync(Guid workScheduleId, CancellationToken ct = default)
    {
        var entities = await _repository.GetByWorkScheduleIdAsync(workScheduleId);
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<ShiftDefinitionSummaryDto>> GetActiveShiftsAsync(CancellationToken ct = default)
    {
        var entities = await _repository.GetActiveShiftsAsync();
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<ShiftDefinitionSummaryDto>> GetNightShiftsAsync(CancellationToken ct = default)
    {
        var entities = await _repository.GetNightShiftsAsync();
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<ShiftDefinitionSummaryDto>> GetByTypeAsync(ShiftType type, CancellationToken ct = default)
    {
        var entities = await _repository.GetByTypeAsync(type);
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<ShiftDefinitionSummaryDto>> GetAllAsync(CancellationToken ct = default)
    {
        var entities = await _repository.GetAllAsync();
        return entities.ToSummaryDtoList();
    }

    public async Task<ShiftDefinitionDto> CreateAsync(CreateShiftDefinitionDto dto, Guid tenantId, Guid userId, CancellationToken ct = default)
    {
        var entity = dto.ToEntity(tenantId, userId);
        await _repository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(ct);

        _logger.LogInformation("Shift definition created: {Name}", entity.ShiftName);
        return entity.ToDto();
    }

    public async Task<ShiftDefinitionDto> UpdateAsync(UpdateShiftDefinitionDto dto, Guid userId, CancellationToken ct = default)
    {
        var entity = await _repository.GetByIdAsync(dto.Id);
        if (entity == null)
            throw new ArgumentException($"Shift definition '{dto.Id}' not found.");

        entity.UpdateEntity(dto, userId);
        await _repository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(ct);
        return entity.ToDto();
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await _repository.GetByIdAsync(id);
        if (entity == null)
            throw new ArgumentException($"Shift definition '{id}' not found.");

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
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<ShiftAssignmentService> _logger;

    public ShiftAssignmentService(
        IShiftAssignmentRepository repository,
        IUnitOfWork unitOfWork,
        ILogger<ShiftAssignmentService> logger)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<ShiftAssignmentDto> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await _repository.GetByIdAsync(id);
        if (entity == null)
            throw new ArgumentException($"Shift assignment '{id}' not found.");
        return entity.ToDto();
    }

    public async Task<ShiftAssignmentDto?> GetCurrentAssignmentForEmployeeAsync(Guid employeeId, CancellationToken ct = default)
    {
        var entity = await _repository.GetCurrentAssignmentForEmployeeAsync(employeeId);
        return entity?.ToDto();
    }

    public async Task<IEnumerable<ShiftAssignmentDto>> GetByEmployeeIdAsync(Guid employeeId, CancellationToken ct = default)
    {
        var entities = await _repository.GetByEmployeeIdAsync(employeeId);
        return entities.Select(e => e.ToDto()).ToList();
    }

    public async Task<IEnumerable<ShiftAssignmentDto>> GetByShiftDefinitionIdAsync(Guid shiftDefinitionId, CancellationToken ct = default)
    {
        var entities = await _repository.GetByShiftDefinitionIdAsync(shiftDefinitionId);
        return entities.Select(e => e.ToDto()).ToList();
    }

    public async Task<IEnumerable<ShiftAssignmentDto>> GetActiveAssignmentsAsync(DateTime? asOf = null, CancellationToken ct = default)
    {
        var entities = await _repository.GetActiveAssignmentsAsync(asOf);
        return entities.Select(e => e.ToDto()).ToList();
    }

    public async Task<ShiftAssignmentDto> AssignAsync(CreateShiftAssignmentDto dto, Guid tenantId, Guid userId, CancellationToken ct = default)
    {
        var entity = dto.ToEntity(tenantId, userId);
        await _repository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(ct);

        _logger.LogInformation("Shift assigned to employee {EmployeeId}", dto.EmployeeId);
        return entity.ToDto();
    }

    public async Task<ShiftAssignmentDto> UpdateAsync(UpdateShiftAssignmentDto dto, Guid userId, CancellationToken ct = default)
    {
        var entity = await _repository.GetByIdAsync(dto.Id);
        if (entity == null)
            throw new ArgumentException($"Shift assignment '{dto.Id}' not found.");

        entity.UpdateEntity(dto, userId);
        await _repository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(ct);
        return entity.ToDto();
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await _repository.GetByIdAsync(id);
        if (entity == null)
            throw new ArgumentException($"Shift assignment '{id}' not found.");

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
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<ShiftRotationPlanService> _logger;

    public ShiftRotationPlanService(
        IShiftRotationPlanRepository repository,
        IShiftRotationStageRepository stageRepository,
        IShiftRotationMemberRepository memberRepository,
        IUnitOfWork unitOfWork,
        ILogger<ShiftRotationPlanService> logger)
    {
        _repository = repository;
        _stageRepository = stageRepository;
        _memberRepository = memberRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<ShiftRotationPlanDto> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await _repository.GetByIdAsync(id);
        if (entity == null)
            throw new ArgumentException($"Shift rotation plan '{id}' not found.");
        return entity.ToDto();
    }

    public async Task<IEnumerable<ShiftRotationPlanSummaryDto>> GetAllAsync(CancellationToken ct = default)
    {
        var entities = await _repository.GetAllAsync();
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<ShiftRotationPlanSummaryDto>> GetActivePlansAsync(CancellationToken ct = default)
    {
        var entities = await _repository.GetActivePlansAsync();
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<ShiftRotationPlanSummaryDto>> GetByRotationCycleAsync(ShiftRotationCycle cycle, CancellationToken ct = default)
    {
        var entities = await _repository.GetByRotationCycleAsync(cycle);
        return entities.ToSummaryDtoList();
    }

    public async Task<ShiftRotationPlanDto> GetWithStagesAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await _repository.GetWithStagesAsync(id);
        if (entity == null)
            throw new ArgumentException($"Shift rotation plan '{id}' not found.");
        return entity.ToDto();
    }

    public async Task<ShiftRotationPlanDto> GetWithMembersAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await _repository.GetWithMembersAsync(id);
        if (entity == null)
            throw new ArgumentException($"Shift rotation plan '{id}' not found.");
        return entity.ToDto();
    }

    public async Task<PagedResult<ShiftRotationPlanSummaryDto>> GetPagedAsync(int pageNumber, int pageSize, CancellationToken ct = default)
    {
        var query = _repository.GetQueryable();
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
        var entity = dto.ToEntity(tenantId, userId);
        await _repository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(ct);

        _logger.LogInformation("Shift rotation plan created: {Name}", entity.PlanName);
        return entity.ToDto();
    }

    public async Task<ShiftRotationPlanDto> UpdateAsync(UpdateShiftRotationPlanDto dto, Guid userId, CancellationToken ct = default)
    {
        var entity = await _repository.GetByIdAsync(dto.Id);
        if (entity == null)
            throw new ArgumentException($"Shift rotation plan '{dto.Id}' not found.");

        entity.UpdateEntity(dto, userId);
        await _repository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(ct);
        return entity.ToDto();
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await _repository.GetByIdAsync(id);
        if (entity == null)
            throw new ArgumentException($"Shift rotation plan '{id}' not found.");

        await _repository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(ct);
        return true;
    }

    public async Task<ShiftRotationStageDto> AddStageAsync(CreateShiftRotationStageDto dto, Guid tenantId, Guid userId, CancellationToken ct = default)
    {
        var entity = dto.ToEntity(tenantId, userId);
        await _stageRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(ct);
        return entity.ToDto();
    }

    public async Task<IEnumerable<ShiftRotationStageDto>> GetStagesAsync(Guid planId, CancellationToken ct = default)
    {
        var entities = await _stageRepository.GetByPlanIdAsync(planId);
        return entities.Select(e => e.ToDto()).ToList();
    }

    public async Task<ShiftRotationStageDto> UpdateStageAsync(UpdateShiftRotationStageDto dto, Guid userId, CancellationToken ct = default)
    {
        var entity = await _stageRepository.GetByIdAsync(dto.Id);
        if (entity == null)
            throw new ArgumentException($"Rotation stage '{dto.Id}' not found.");

        entity.UpdateEntity(dto, userId);
        await _stageRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(ct);
        return entity.ToDto();
    }

    public async Task<bool> DeleteStageAsync(Guid stageId, CancellationToken ct = default)
    {
        var entity = await _stageRepository.GetByIdAsync(stageId);
        if (entity == null)
            throw new ArgumentException($"Rotation stage '{stageId}' not found.");

        await _stageRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(ct);
        return true;
    }

    public async Task<ShiftRotationMemberDto> EnrollMemberAsync(AddShiftRotationMemberDto dto, Guid tenantId, Guid userId, CancellationToken ct = default)
    {
        var entity = dto.ToEntity(tenantId, userId);
        await _memberRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(ct);

        _logger.LogInformation("Employee {EmployeeId} enrolled in rotation plan {PlanId}", dto.EmployeeId, dto.ShiftRotationPlanId);
        return entity.ToDto();
    }

    public async Task<IEnumerable<ShiftRotationMemberDto>> GetMembersAsync(Guid planId, CancellationToken ct = default)
    {
        var entities = await _memberRepository.GetByPlanIdAsync(planId);
        return entities.Select(e => e.ToDto()).ToList();
    }

    public async Task<ShiftRotationMemberDto> UpdateMemberAsync(UpdateShiftRotationMemberDto dto, Guid userId, CancellationToken ct = default)
    {
        var entity = await _memberRepository.GetByIdAsync(dto.Id);
        if (entity == null)
            throw new ArgumentException($"Rotation member '{dto.Id}' not found.");

        entity.UpdateEntity(dto, userId);
        await _memberRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(ct);
        return entity.ToDto();
    }

    public async Task<bool> RemoveMemberAsync(Guid memberId, Guid userId, CancellationToken ct = default)
    {
        var entity = await _memberRepository.GetByIdAsync(memberId);
        if (entity == null)
            throw new ArgumentException($"Rotation member '{memberId}' not found.");

        await _memberRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(ct);
        return true;
    }
}

#endregion
