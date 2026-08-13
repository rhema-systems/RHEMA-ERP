using ErpSystem.Core.Entities.HR.Recruitment;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Interfaces.HR;

// ============================================================================
// ONBOARDING PLAN TEMPLATE
// ============================================================================

#region Onboarding Plan Template

public interface IOnboardingPlanTemplateRepository : IGenericRepository<OnboardingPlanTemplate>
{
    /// <summary>Returns all active onboarding plan templates.</summary>
    Task<IEnumerable<OnboardingPlanTemplate>> GetActiveTemplatesAsync();

    /// <summary>Returns the default template with its task templates loaded, or null if none is set.</summary>
    Task<OnboardingPlanTemplate?> GetDefaultTemplateAsync();

    /// <summary>Returns a template with all its task templates loaded.</summary>
    Task<OnboardingPlanTemplate?> GetWithTaskTemplatesAsync(Guid id);

    // GetByPositionIdAsync was removed — templates carry no position link, so it ignored its argument.
    // See the note in OnboardingRepositories for the applicability-rule design that replaces it.
}

#endregion

// ============================================================================
// ONBOARDING TASK TEMPLATE
// ============================================================================

#region Onboarding Task Template

public interface IOnboardingTaskTemplateRepository : IGenericRepository<OnboardingTaskTemplate>
{
    /// <summary>Returns all task templates for a plan template, ordered by day offset and sort order.</summary>
    Task<IEnumerable<OnboardingTaskTemplate>> GetByPlanTemplateIdAsync(Guid planTemplateId);
}

#endregion

// ============================================================================
// ONBOARDING PLAN
// ============================================================================

#region Onboarding Plan

public interface IOnboardingPlanRepository : IGenericRepository<OnboardingPlan>
{
    /// <summary>Returns all onboarding plans for an employee, ordered by start date descending.</summary>
    Task<IEnumerable<OnboardingPlan>> GetByEmployeeIdAsync(Guid employeeId);

    /// <summary>Returns a fully-loaded onboarding plan including all tasks, assets, comments, and employee details.</summary>
    Task<OnboardingPlan?> GetWithFullDetailsAsync(Guid id);

    /// <summary>Returns onboarding plans filtered by status.</summary>
    Task<IEnumerable<OnboardingPlan>> GetByStatusAsync(OnboardingStatus status);

    /// <summary>Returns the count of overdue tasks for an onboarding plan.</summary>
    Task<int> GetOverdueTasksCountAsync(Guid planId);
}

#endregion

// ============================================================================
// ONBOARDING TASK
// ============================================================================

#region Onboarding Task

public interface IOnboardingTaskRepository : IGenericRepository<OnboardingTask>
{
    /// <summary>Returns all tasks for an onboarding plan, ordered by due date.</summary>
    Task<IEnumerable<OnboardingTask>> GetByPlanIdAsync(Guid planId);

    /// <summary>Returns tasks filtered by status, optionally scoped to a plan.</summary>
    Task<IEnumerable<OnboardingTask>> GetByStatusAsync(OnboardingTaskStatus status, Guid? planId = null);

    /// <summary>Returns tasks whose due date has passed and are not yet completed.</summary>
    Task<IEnumerable<OnboardingTask>> GetOverdueTasksAsync();

    /// <summary>Returns tasks assigned to a specific employee, with plan and employee details loaded.</summary>
    Task<IEnumerable<OnboardingTask>> GetByAssignedToAsync(Guid employeeId);
}

#endregion

// ============================================================================
// ONBOARDING TASK COMMENT
// ============================================================================

#region Onboarding Task Comment

public interface IOnboardingTaskCommentRepository : IGenericRepository<OnboardingTaskComment>
{
    /// <summary>Returns all comments for an onboarding task, ordered by created date.</summary>
    Task<IEnumerable<OnboardingTaskComment>> GetByTaskIdAsync(Guid taskId);
}

#endregion

// ============================================================================
// ONBOARDING ASSET
// ============================================================================

#region Onboarding Asset

public interface IOnboardingAssetRepository : IGenericRepository<OnboardingAsset>
{
    /// <summary>Returns all asset provision items for an onboarding plan.</summary>
    Task<IEnumerable<OnboardingAsset>> GetByPlanIdAsync(Guid planId);

    /// <summary>Returns asset provision items filtered by provision status, optionally scoped to a plan.</summary>
    Task<IEnumerable<OnboardingAsset>> GetByStatusAsync(OnboardingAssetProvisionStatus status, Guid? planId = null);
}

#endregion
