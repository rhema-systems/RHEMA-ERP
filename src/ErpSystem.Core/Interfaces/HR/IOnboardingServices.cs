using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Interfaces.HR;

// ============================================================================
// ONBOARDING PLAN TEMPLATE SERVICE
// ============================================================================

public interface IOnboardingPlanTemplateService
{
    Task<OnboardingPlanTemplateDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IEnumerable<OnboardingPlanTemplateSummaryDto>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<OnboardingPlanTemplateDetailDto> GetWithTaskTemplatesAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IEnumerable<OnboardingPlanTemplateSummaryDto>> GetByPositionIdAsync(Guid positionId, CancellationToken cancellationToken = default);
    Task<OnboardingPlanTemplateDto?> GetDefaultAsync(CancellationToken cancellationToken = default);

    // CRUD
    Task<OnboardingPlanTemplateDto> CreateAsync(CreateOnboardingPlanTemplateDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default);
    Task<OnboardingPlanTemplateDto> UpdateAsync(UpdateOnboardingPlanTemplateDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    // Task templates
    Task<OnboardingTaskTemplateDto> AddTaskTemplateAsync(CreateOnboardingTaskTemplateDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default);
    Task<IEnumerable<OnboardingTaskTemplateDto>> GetTaskTemplatesAsync(Guid planTemplateId, CancellationToken cancellationToken = default);
    Task<OnboardingTaskTemplateDto> UpdateTaskTemplateAsync(UpdateOnboardingTaskTemplateDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default);
    Task<bool> DeleteTaskTemplateAsync(Guid taskTemplateId, CancellationToken cancellationToken = default);
}

// ============================================================================
// ONBOARDING PLAN SERVICE
// ============================================================================

public interface IOnboardingPlanService
{
    // Queries
    Task<OnboardingPlanDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<OnboardingPlanDto?> GetByEmployeeIdAsync(Guid employeeId, CancellationToken cancellationToken = default);
    Task<OnboardingPlanDetailDto> GetWithFullDetailsAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IEnumerable<OnboardingPlanSummaryDto>> GetByStatusAsync(OnboardingStatus status, CancellationToken cancellationToken = default);

    // CRUD
    Task<OnboardingPlanDto> CreateAsync(CreateOnboardingPlanDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default);
    Task<OnboardingPlanDto> UpdateAsync(UpdateOnboardingPlanDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default);

    // Workflow
    Task<bool> StartAsync(Guid planId, Guid updatedByUserId, CancellationToken cancellationToken = default);
    Task<bool> CompleteAsync(Guid planId, Guid updatedByUserId, CancellationToken cancellationToken = default);
    Task<int> GetOverdueTasksCountAsync(Guid planId, CancellationToken cancellationToken = default);

    // Tasks
    Task<IEnumerable<OnboardingTaskDto>> GetTasksAsync(Guid planId, CancellationToken cancellationToken = default);
    Task<OnboardingTaskDto> AddTaskAsync(CreateOnboardingTaskDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default);
    Task<OnboardingTaskDto> UpdateTaskAsync(UpdateOnboardingTaskDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default);
    Task<bool> CompleteTaskAsync(Guid taskId, Guid completedByUserId, CancellationToken cancellationToken = default);

    // Asset items
    Task<IEnumerable<OnboardingAssetDto>> GetAssetItemsAsync(Guid planId, CancellationToken cancellationToken = default);
    Task<OnboardingAssetDto> AddAssetItemAsync(CreateOnboardingAssetDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default);
    Task<OnboardingAssetDto> UpdateAssetItemAsync(UpdateOnboardingAssetDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default);

    // Task comments
    Task<OnboardingTaskCommentDto> AddTaskCommentAsync(CreateOnboardingTaskCommentDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default);
    Task<IEnumerable<OnboardingTaskCommentDto>> GetTaskCommentsAsync(Guid taskId, CancellationToken cancellationToken = default);

    // Extended queries
    Task<IEnumerable<OnboardingTaskDto>> GetTasksByStatusAsync(OnboardingTaskStatus status, Guid? planId = null, CancellationToken cancellationToken = default);
    Task<IEnumerable<OnboardingTaskDto>> GetOverdueTasksAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<OnboardingTaskDto>> GetTasksByAssigneeAsync(Guid employeeId, CancellationToken cancellationToken = default);
    Task<IEnumerable<OnboardingAssetDto>> GetAssetsByStatusAsync(OnboardingAssetProvisionStatus status, Guid? planId = null, CancellationToken cancellationToken = default);
}
