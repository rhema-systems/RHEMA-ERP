using ErpSystem.Application.HR.Extensions;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR;

// ============================================================================
// ONBOARDING PLAN TEMPLATE SERVICE
// ============================================================================

public class OnboardingPlanTemplateService : IOnboardingPlanTemplateService
{
    private readonly IOnboardingPlanTemplateRepository _templateRepository;
    private readonly IOnboardingTaskTemplateRepository _taskTemplateRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<OnboardingPlanTemplateService> _logger;

    public OnboardingPlanTemplateService(
        IOnboardingPlanTemplateRepository templateRepository,
        IOnboardingTaskTemplateRepository taskTemplateRepository,
        IUnitOfWork unitOfWork,
        ILogger<OnboardingPlanTemplateService> logger)
    {
        _templateRepository = templateRepository;
        _taskTemplateRepository = taskTemplateRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<OnboardingPlanTemplateDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _templateRepository.GetByIdAsync(id);
        if (entity == null)
            throw new ArgumentException($"Onboarding plan template with ID '{id}' not found.");
        return entity.ToDto();
    }

    public async Task<IEnumerable<OnboardingPlanTemplateSummaryDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var entities = await _templateRepository.GetActiveTemplatesAsync();
        return entities.ToSummaryDtoList();
    }

    public async Task<OnboardingPlanTemplateDetailDto> GetWithTaskTemplatesAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _templateRepository.GetWithTaskTemplatesAsync(id);
        if (entity == null)
            throw new ArgumentException($"Onboarding plan template with ID '{id}' not found.");
        return entity.ToDetailDto();
    }

    public async Task<IEnumerable<OnboardingPlanTemplateSummaryDto>> GetByPositionIdAsync(Guid positionId, CancellationToken cancellationToken = default)
    {
        var entities = await _templateRepository.GetByPositionIdAsync(positionId);
        return entities.ToSummaryDtoList();
    }

    public async Task<OnboardingPlanTemplateDto?> GetDefaultAsync(CancellationToken cancellationToken = default)
    {
        var entity = await _templateRepository.GetDefaultTemplateAsync();
        return entity?.ToDto();
    }

    public async Task<OnboardingPlanTemplateDto> CreateAsync(CreateOnboardingPlanTemplateDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        if (createDto.IsDefault)
        {
            var existingDefault = await _templateRepository.GetDefaultTemplateAsync();
            if (existingDefault != null)
            {
                existingDefault.IsDefault = false;
                await _templateRepository.UpdateAsync(existingDefault);
            }
        }

        var entity = createDto.ToEntity(tenantId, createdByUserId);
        await _templateRepository.AddAsync(entity);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Onboarding plan template created: {Name}", entity.Name);
        return entity.ToDto();
    }

    public async Task<OnboardingPlanTemplateDto> UpdateAsync(UpdateOnboardingPlanTemplateDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await _templateRepository.GetByIdAsync(updateDto.Id);
        if (entity == null)
            throw new ArgumentException($"Onboarding plan template with ID '{updateDto.Id}' not found.");

        if (updateDto.IsDefault && !entity.IsDefault)
        {
            var existingDefault = await _templateRepository.GetDefaultTemplateAsync();
            if (existingDefault != null && existingDefault.Id != entity.Id)
            {
                existingDefault.IsDefault = false;
                await _templateRepository.UpdateAsync(existingDefault);
            }
        }

        entity.UpdateEntity(updateDto, updatedByUserId);
        await _templateRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _templateRepository.GetByIdAsync(id);
        if (entity == null)
            throw new ArgumentException($"Onboarding plan template with ID '{id}' not found.");

        if (entity.IsDefault)
            throw new InvalidOperationException("The default onboarding template cannot be deleted. Assign a new default first.");

        await _templateRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<OnboardingTaskTemplateDto> AddTaskTemplateAsync(CreateOnboardingTaskTemplateDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        var entity = createDto.ToEntity(tenantId, createdByUserId);
        await _taskTemplateRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<IEnumerable<OnboardingTaskTemplateDto>> GetTaskTemplatesAsync(Guid planTemplateId, CancellationToken cancellationToken = default)
    {
        var entities = await _taskTemplateRepository.GetByPlanTemplateIdAsync(planTemplateId);
        return entities.Select(e => e.ToDto());
    }

    public async Task<OnboardingTaskTemplateDto> UpdateTaskTemplateAsync(UpdateOnboardingTaskTemplateDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await _taskTemplateRepository.GetByIdAsync(updateDto.Id);
        if (entity == null)
            throw new ArgumentException($"Task template with ID '{updateDto.Id}' not found.");

        entity.UpdateEntity(updateDto, updatedByUserId);
        await _taskTemplateRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<bool> DeleteTaskTemplateAsync(Guid taskTemplateId, CancellationToken cancellationToken = default)
    {
        var entity = await _taskTemplateRepository.GetByIdAsync(taskTemplateId);
        if (entity == null)
            throw new ArgumentException($"Task template with ID '{taskTemplateId}' not found.");

        await _taskTemplateRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }
}

// ============================================================================
// ONBOARDING PLAN SERVICE
// ============================================================================

public class OnboardingPlanService : IOnboardingPlanService
{
    private readonly IOnboardingPlanRepository _planRepository;
    private readonly IOnboardingTaskRepository _taskRepository;
    private readonly IOnboardingAssetRepository _assetItemRepository;
    private readonly IOnboardingTaskCommentRepository _commentRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<OnboardingPlanService> _logger;

    public OnboardingPlanService(
        IOnboardingPlanRepository planRepository,
        IOnboardingTaskRepository taskRepository,
        IOnboardingAssetRepository assetItemRepository,
        IOnboardingTaskCommentRepository commentRepository,
        IUnitOfWork unitOfWork,
        ILogger<OnboardingPlanService> logger)
    {
        _planRepository = planRepository;
        _taskRepository = taskRepository;
        _assetItemRepository = assetItemRepository;
        _commentRepository = commentRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<OnboardingPlanDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _planRepository.GetByIdAsync(id);
        if (entity == null)
            throw new ArgumentException($"Onboarding plan with ID '{id}' not found.");
        return entity.ToDto();
    }

    public async Task<OnboardingPlanDto?> GetByEmployeeIdAsync(Guid employeeId, CancellationToken cancellationToken = default)
    {
        var entities = await _planRepository.GetByEmployeeIdAsync(employeeId);
        var active = entities.FirstOrDefault(p => p.Status == OnboardingStatus.InProgress)
            ?? entities.OrderByDescending(p => p.CreatedAt).FirstOrDefault();
        return active?.ToDto();
    }

    public async Task<OnboardingPlanDetailDto> GetWithFullDetailsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _planRepository.GetWithFullDetailsAsync(id);
        if (entity == null)
            throw new ArgumentException($"Onboarding plan with ID '{id}' not found.");
        return entity.ToDetailDto();
    }

    public async Task<IEnumerable<OnboardingPlanSummaryDto>> GetByStatusAsync(OnboardingStatus status, CancellationToken cancellationToken = default)
    {
        var entities = await _planRepository.GetByStatusAsync(status);
        return entities.ToSummaryDtoList();
    }

    public async Task<OnboardingPlanDto> CreateAsync(CreateOnboardingPlanDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        var entity = createDto.ToEntity(tenantId, createdByUserId);
        entity.Status = OnboardingStatus.NotStarted;

        await _planRepository.AddAsync(entity);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Onboarding plan created for employee {EmployeeId}", createDto.EmployeeId);
        return entity.ToDto();
    }

    public async Task<OnboardingPlanDto> UpdateAsync(UpdateOnboardingPlanDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await _planRepository.GetByIdAsync(updateDto.Id);
        if (entity == null)
            throw new ArgumentException($"Onboarding plan with ID '{updateDto.Id}' not found.");

        if (entity.Status == OnboardingStatus.Completed)
            throw new InvalidOperationException("A completed onboarding plan cannot be edited.");

        entity.UpdateEntity(updateDto, updatedByUserId);
        await _planRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<bool> StartAsync(Guid planId, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await _planRepository.GetByIdAsync(planId);
        if (entity == null)
            throw new ArgumentException($"Onboarding plan with ID '{planId}' not found.");

        entity.Status = OnboardingStatus.InProgress;

        await _planRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> CompleteAsync(Guid planId, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await _planRepository.GetByIdAsync(planId);
        if (entity == null)
            throw new ArgumentException($"Onboarding plan with ID '{planId}' not found.");

        entity.Status = OnboardingStatus.Completed;
        entity.ActualCompletionDate = DateOnly.FromDateTime(DateTime.UtcNow);

        await _planRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<int> GetOverdueTasksCountAsync(Guid planId, CancellationToken cancellationToken = default)
    {
        return await _planRepository.GetOverdueTasksCountAsync(planId);
    }

    // ── Tasks ─────────────────────────────────────────────────────────────────

    public async Task<IEnumerable<OnboardingTaskDto>> GetTasksAsync(Guid planId, CancellationToken cancellationToken = default)
    {
        var entities = await _taskRepository.GetByPlanIdAsync(planId);
        return entities.Select(e => e.ToDto());
    }

    public async Task<OnboardingTaskDto> AddTaskAsync(CreateOnboardingTaskDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        var entity = createDto.ToEntity(tenantId, createdByUserId);
        await _taskRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<OnboardingTaskDto> UpdateTaskAsync(UpdateOnboardingTaskDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await _taskRepository.GetByIdAsync(updateDto.Id);
        if (entity == null)
            throw new ArgumentException($"Onboarding task with ID '{updateDto.Id}' not found.");

        entity.UpdateEntity(updateDto, updatedByUserId);
        await _taskRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<bool> CompleteTaskAsync(Guid taskId, Guid completedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await _taskRepository.GetByIdAsync(taskId);
        if (entity == null)
            throw new ArgumentException($"Onboarding task with ID '{taskId}' not found.");

        entity.Status = OnboardingTaskStatus.Completed;
        entity.CompletedDate = DateOnly.FromDateTime(DateTime.UtcNow);
        entity.CompletedById = completedByUserId;

        await _taskRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    // ── Asset items ───────────────────────────────────────────────────────────

    public async Task<IEnumerable<OnboardingAssetDto>> GetAssetItemsAsync(Guid planId, CancellationToken cancellationToken = default)
    {
        var entities = await _assetItemRepository.GetByPlanIdAsync(planId);
        return entities.Select(e => e.ToDto());
    }

    public async Task<OnboardingAssetDto> AddAssetItemAsync(CreateOnboardingAssetDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        var entity = createDto.ToEntity(tenantId, createdByUserId);
        await _assetItemRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<OnboardingAssetDto> UpdateAssetItemAsync(UpdateOnboardingAssetDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await _assetItemRepository.GetByIdAsync(updateDto.Id);
        if (entity == null)
            throw new ArgumentException($"Onboarding asset item with ID '{updateDto.Id}' not found.");

        entity.UpdateEntity(updateDto, updatedByUserId);
        await _assetItemRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    // ── Task comments ─────────────────────────────────────────────────────────

    public async Task<OnboardingTaskCommentDto> AddTaskCommentAsync(CreateOnboardingTaskCommentDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        var entity = createDto.ToEntity(tenantId, createdByUserId);
        await _commentRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<IEnumerable<OnboardingTaskCommentDto>> GetTaskCommentsAsync(Guid taskId, CancellationToken cancellationToken = default)
    {
        var entities = await _commentRepository.GetByTaskIdAsync(taskId);
        return entities.Select(e => e.ToDto());
    }

    public async Task<IEnumerable<OnboardingTaskDto>> GetTasksByStatusAsync(OnboardingTaskStatus status, Guid? planId = null, CancellationToken cancellationToken = default)
    {
        var entities = await _taskRepository.GetByStatusAsync(status, planId);
        return entities.Select(e => e.ToDto());
    }

    public async Task<IEnumerable<OnboardingTaskDto>> GetOverdueTasksAsync(CancellationToken cancellationToken = default)
    {
        var entities = await _taskRepository.GetOverdueTasksAsync();
        return entities.Select(e => e.ToDto());
    }

    public async Task<IEnumerable<OnboardingTaskDto>> GetTasksByAssigneeAsync(Guid employeeId, CancellationToken cancellationToken = default)
    {
        var entities = await _taskRepository.GetByAssignedToAsync(employeeId);
        return entities.Select(e => e.ToDto());
    }

    public async Task<IEnumerable<OnboardingAssetDto>> GetAssetsByStatusAsync(OnboardingAssetProvisionStatus status, Guid? planId = null, CancellationToken cancellationToken = default)
    {
        var entities = await _assetItemRepository.GetByStatusAsync(status, planId);
        return entities.Select(e => e.ToDto());
    }
}
