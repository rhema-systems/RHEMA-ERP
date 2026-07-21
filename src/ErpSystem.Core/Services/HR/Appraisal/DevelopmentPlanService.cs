using ErpSystem.Application.HR.Extensions;
using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Entities.HR.Performance;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR;

public class DevelopmentPlanService : IDevelopmentPlanService
{
    private readonly IGenericRepository<EmployeeDevelopmentPlan> _planRepository;
    private readonly IGenericRepository<EmployeeDevelopmentObjective> _objectiveRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<DevelopmentPlanService> _logger;

    public DevelopmentPlanService(
        IGenericRepository<EmployeeDevelopmentPlan> planRepository,
        IGenericRepository<EmployeeDevelopmentObjective> objectiveRepository,
        IUnitOfWork unitOfWork,
        ILogger<DevelopmentPlanService> logger)
    {
        _planRepository = planRepository;
        _objectiveRepository = objectiveRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    private IQueryable<EmployeeDevelopmentPlan> BaseQuery => _planRepository.GetQueryable()
        .Include(p => p.Employee)
        .Include(p => p.Cycle)
        .Include(p => p.Objectives);

    public async Task<EmployeeDevelopmentPlanDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await BaseQuery.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (entity == null)
            throw new ArgumentException($"Development plan with ID '{id}' not found.");
        return entity.ToDto();
    }

    public async Task<IEnumerable<EmployeeDevelopmentPlanDto>> GetByEmployeeIdAsync(Guid employeeId, CancellationToken cancellationToken = default)
    {
        var entities = await BaseQuery
            .Where(p => p.EmployeeId == employeeId)
            .OrderByDescending(p => p.StartDate)
            .ToListAsync(cancellationToken);
        return entities.ToDtoList();
    }

    public async Task<IEnumerable<EmployeeDevelopmentPlanDto>> GetByManagerIdAsync(Guid managerId, CancellationToken cancellationToken = default)
    {
        var entities = await BaseQuery
            .Where(p => p.Employee.ManagerId == managerId)
            .OrderBy(p => p.Employee.LastName)
            .ThenBy(p => p.Employee.FirstName)
            .ThenByDescending(p => p.StartDate)
            .ToListAsync(cancellationToken);
        return entities.ToDtoList();
    }

    public async Task<EmployeeDevelopmentPlanDto?> GetActivePlanAsync(Guid employeeId, Guid? cycleId = null, CancellationToken cancellationToken = default)
    {
        var query = BaseQuery.Where(p =>
            p.EmployeeId == employeeId &&
            p.PlanStatus == DevelopmentPlanStatus.Active);

        if (cycleId.HasValue)
            query = query.Where(p => p.AppraisalCycleId == cycleId.Value);

        var entity = await query.OrderByDescending(p => p.StartDate).FirstOrDefaultAsync(cancellationToken);
        return entity?.ToDto();
    }

    public async Task<PagedResult<EmployeeDevelopmentPlanDto>> GetPagedAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default)
    {
        var query = BaseQuery.OrderByDescending(p => p.StartDate);
        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        return new PagedResult<EmployeeDevelopmentPlanDto>
        {
            Items = items.ToDtoList(),
            TotalCount = totalCount,
            Page = pageNumber,
            PageSize = pageSize
        };
    }

    public async Task<EmployeeDevelopmentPlanDto> CreateAsync(CreateEmployeeDevelopmentPlanDto createDto, CancellationToken cancellationToken = default)
    {
        var entity = createDto.ToEntity();
        entity.PlanStatus = DevelopmentPlanStatus.Active;

        await _planRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Development plan created: {Id} for employee {EmployeeId}", entity.Id, entity.EmployeeId);
        return await GetByIdAsync(entity.Id, cancellationToken);
    }

    public async Task<EmployeeDevelopmentPlanDto> UpdateAsync(UpdateEmployeeDevelopmentPlanDto updateDto, CancellationToken cancellationToken = default)
    {
        var entity = await _planRepository.GetByIdAsync(updateDto.Id);
        if (entity == null)
            throw new ArgumentException($"Development plan with ID '{updateDto.Id}' not found.");

        if (entity.PlanStatus == DevelopmentPlanStatus.Completed)
            throw new InvalidOperationException("Cannot update a completed development plan.");

        updateDto.UpdateEntity(entity);
        await _planRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Development plan updated: {Id}", entity.Id);
        return await GetByIdAsync(entity.Id, cancellationToken);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _planRepository.GetByIdAsync(id);
        if (entity == null)
            throw new ArgumentException($"Development plan with ID '{id}' not found.");

        await _planRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Development plan deleted: {Id}", id);
        return true;
    }

    public async Task<bool> UpdateStatusAsync(Guid id, DevelopmentPlanStatus status, CancellationToken cancellationToken = default)
    {
        var entity = await _planRepository.GetByIdAsync(id);
        if (entity == null)
            throw new ArgumentException($"Development plan with ID '{id}' not found.");

        entity.PlanStatus = status;
        await _planRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Development plan {Id} status updated to {Status}", id, status);
        return true;
    }

    // ─── Objectives ───────────────────────────────────────────────────────────

    public async Task<EmployeeDevelopmentObjectiveDto> AddObjectiveAsync(Guid planId, CreateEmployeeDevelopmentObjectiveDto dto, CancellationToken cancellationToken = default)
    {
        var planExists = await _planRepository.ExistsAsync(p => p.Id == planId);
        if (!planExists)
            throw new ArgumentException("Development plan not found.");

        var entity = dto.ToEntity();
        entity.DevelopmentPlanId = planId;
        entity.ObjectiveStatus = DevelopmentObjectiveStatus.NotStarted;

        await _objectiveRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Development objective added to plan {PlanId}: {ObjectiveId}", planId, entity.Id);

        entity = await _objectiveRepository.GetByIdAsync(entity.Id);
        return entity!.ToDto();
    }

    public async Task<IEnumerable<EmployeeDevelopmentObjectiveDto>> GetObjectivesAsync(Guid planId, CancellationToken cancellationToken = default)
    {
        var entities = await _objectiveRepository
            .GetQueryable(o => o.DevelopmentPlanId == planId)
            .OrderBy(o => o.Title)
            .ToListAsync(cancellationToken);
        return entities.ToDtoList();
    }

    public async Task<EmployeeDevelopmentObjectiveDto> UpdateObjectiveAsync(Guid planId, UpdateEmployeeDevelopmentObjectiveDto dto, CancellationToken cancellationToken = default)
    {
        var entity = await _objectiveRepository.GetQueryable()
            .FirstOrDefaultAsync(o => o.Id == dto.Id && o.DevelopmentPlanId == planId, cancellationToken);

        if (entity == null)
            throw new ArgumentException("Development objective not found.");

        dto.UpdateEntity(entity);
        await _objectiveRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Development objective updated: {ObjectiveId}", entity.Id);
        return entity.ToDto();
    }

    public async Task<bool> DeleteObjectiveAsync(Guid planId, Guid objectiveId, CancellationToken cancellationToken = default)
    {
        var entity = await _objectiveRepository.GetQueryable()
            .FirstOrDefaultAsync(o => o.Id == objectiveId && o.DevelopmentPlanId == planId, cancellationToken);

        if (entity == null)
            throw new ArgumentException("Development objective not found.");

        await _objectiveRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<EmployeeDevelopmentObjectiveDto> UpdateObjectiveProgressAsync(
        Guid planId, Guid objectiveId, decimal progressPercent, string? notes,
        DevelopmentObjectiveStatus status, CancellationToken cancellationToken = default)
    {
        var entity = await _objectiveRepository.GetQueryable()
            .FirstOrDefaultAsync(o => o.Id == objectiveId && o.DevelopmentPlanId == planId, cancellationToken);

        if (entity == null)
            throw new ArgumentException("Development objective not found.");

        var clampedProgress = Math.Clamp(progressPercent, 0, 100);
        entity.ProgressPercent = clampedProgress;

        if (!string.IsNullOrWhiteSpace(notes))
            entity.ProgressNotes = notes;

        entity.ObjectiveStatus = status;

        await _objectiveRepository.UpdateAsync(entity);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Development objective {ObjectiveId} progress updated to {Progress}%", objectiveId, clampedProgress);
        return entity.ToDto();
    }
}
