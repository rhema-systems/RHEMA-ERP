using ErpSystem.Core.Entities.HR.Recruitment;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Data.Repositories.HR;

// ============================================================================
// ONBOARDING PLAN TEMPLATE REPOSITORY
// ============================================================================

#region Onboarding Plan Template Repository

public class OnboardingPlanTemplateRepository : GenericRepository<OnboardingPlanTemplate>, IOnboardingPlanTemplateRepository
{
    public OnboardingPlanTemplateRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<OnboardingPlanTemplate>> GetActiveTemplatesAsync()
    {
        return await _dbSet
            .Include(t => t.TaskTemplates.Where(tt => !tt.IsDeleted))
            .Where(t => t.IsActive && !t.IsDeleted)
            .OrderBy(t => t.Name)
            .ToListAsync();
    }

    public async Task<OnboardingPlanTemplate?> GetDefaultTemplateAsync()
    {
        return await _dbSet
            .Include(t => t.TaskTemplates.Where(tt => !tt.IsDeleted).OrderBy(tt => tt.DueDaysFromStartDate).ThenBy(tt => tt.DisplayOrder))
            .FirstOrDefaultAsync(t => t.IsDefault && t.IsActive && !t.IsDeleted);
    }

    public async Task<OnboardingPlanTemplate?> GetWithTaskTemplatesAsync(Guid id)
    {
        return await _dbSet
            .Include(t => t.TaskTemplates.Where(tt => !tt.IsDeleted).OrderBy(tt => tt.DueDaysFromStartDate).ThenBy(tt => tt.DisplayOrder))
            .FirstOrDefaultAsync(t => t.Id == id && !t.IsDeleted);
    }

    public async Task<IEnumerable<OnboardingPlanTemplate>> GetByPositionIdAsync(Guid positionId)
    {
        return await _dbSet
            .Include(t => t.TaskTemplates.Where(tt => !tt.IsDeleted))
            .Where(t => t.IsActive && !t.IsDeleted)
            .OrderBy(t => t.Name)
            .ToListAsync();
    }
}

#endregion

// ============================================================================
// ONBOARDING TASK TEMPLATE REPOSITORY
// ============================================================================

#region Onboarding Task Template Repository

public class OnboardingTaskTemplateRepository : GenericRepository<OnboardingTaskTemplate>, IOnboardingTaskTemplateRepository
{
    public OnboardingTaskTemplateRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<OnboardingTaskTemplate>> GetByPlanTemplateIdAsync(Guid planTemplateId)
    {
        return await _dbSet
            .Where(t => t.PlanTemplateId == planTemplateId && !t.IsDeleted)
            .OrderBy(t => t.DueDaysFromStartDate)
            .ThenBy(t => t.DisplayOrder)
            .ToListAsync();
    }
}

#endregion

// ============================================================================
// ONBOARDING PLAN REPOSITORY
// ============================================================================

#region Onboarding Plan Repository

public class OnboardingPlanRepository : GenericRepository<OnboardingPlan>, IOnboardingPlanRepository
{
    public OnboardingPlanRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<OnboardingPlan>> GetByEmployeeIdAsync(Guid employeeId)
    {
        return await _dbSet
            .Include(p => p.Employee)
            .Where(p => p.EmployeeId == employeeId && !p.IsDeleted)
            .OrderByDescending(p => p.StartDate)
            .ToListAsync();
    }

    public async Task<OnboardingPlan?> GetWithFullDetailsAsync(Guid id)
    {
        return await _dbSet
            .Include(p => p.Employee)
            .Include(p => p.TemplatePlan)
            .Include(p => p.Tasks).ThenInclude(t => t.AssignedTo)
            .Include(p => p.Tasks).ThenInclude(t => t.Comments)
            .Include(p => p.Assets)
            .FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted);
    }

    public async Task<IEnumerable<OnboardingPlan>> GetByStatusAsync(OnboardingStatus status)
    {
        return await _dbSet
            .Include(p => p.Employee)
            .Where(p => p.Status == status && !p.IsDeleted)
            .OrderByDescending(p => p.StartDate)
            .ToListAsync();
    }

    public async Task<int> GetOverdueTasksCountAsync(Guid planId)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        return await _dbSet
            .Where(p => p.Id == planId && !p.IsDeleted)
            .SelectMany(p => p.Tasks)
            .CountAsync(t => !t.IsDeleted
                          && t.Status != OnboardingTaskStatus.Completed
                          && t.DueDate < today);
    }
}

#endregion

// ============================================================================
// ONBOARDING TASK REPOSITORY
// ============================================================================

#region Onboarding Task Repository

public class OnboardingTaskRepository : GenericRepository<OnboardingTask>, IOnboardingTaskRepository
{
    public OnboardingTaskRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<OnboardingTask>> GetByPlanIdAsync(Guid planId)
    {
        return await _dbSet
            .Include(t => t.AssignedTo)
            .Where(t => t.OnboardingPlanId == planId && !t.IsDeleted)
            .OrderBy(t => t.DueDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<OnboardingTask>> GetByStatusAsync(OnboardingTaskStatus status, Guid? planId = null)
    {
        var query = _dbSet
            .Include(t => t.AssignedTo)
            .Include(t => t.OnboardingPlan).ThenInclude(p => p.Employee)
            .Where(t => t.Status == status && !t.IsDeleted);

        if (planId.HasValue)
            query = query.Where(t => t.OnboardingPlanId == planId.Value);

        return await query
            .OrderBy(t => t.DueDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<OnboardingTask>> GetOverdueTasksAsync()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        return await _dbSet
            .Include(t => t.AssignedTo)
            .Include(t => t.OnboardingPlan).ThenInclude(p => p.Employee)
            .Where(t => !t.IsDeleted
                     && t.Status != OnboardingTaskStatus.Completed
                     && t.DueDate < today)
            .OrderBy(t => t.DueDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<OnboardingTask>> GetByAssignedToAsync(Guid employeeId)
    {
        return await _dbSet
            .Include(t => t.OnboardingPlan).ThenInclude(p => p.Employee)
            .Where(t => t.AssignedToId == employeeId && !t.IsDeleted)
            .OrderBy(t => t.DueDate)
            .ToListAsync();
    }
}

#endregion

// ============================================================================
// ONBOARDING TASK COMMENT REPOSITORY
// ============================================================================

#region Onboarding Task Comment Repository

public class OnboardingTaskCommentRepository : GenericRepository<OnboardingTaskComment>, IOnboardingTaskCommentRepository
{
    public OnboardingTaskCommentRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<OnboardingTaskComment>> GetByTaskIdAsync(Guid taskId)
    {
        return await _dbSet
            .Include(c => c.Author)
            .Where(c => c.TaskId == taskId && !c.IsDeleted)
            .OrderBy(c => c.CreatedAt)
            .ToListAsync();
    }
}

#endregion

// ============================================================================
// ONBOARDING ASSET REPOSITORY
// ============================================================================

#region Onboarding Asset Repository

public class OnboardingAssetRepository : GenericRepository<OnboardingAsset>, IOnboardingAssetRepository
{
    public OnboardingAssetRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<OnboardingAsset>> GetByPlanIdAsync(Guid planId)
    {
        return await _dbSet
            .Include(a => a.ProvisionedBy)
            .Where(a => a.OnboardingPlanId == planId && !a.IsDeleted)
            .OrderBy(a => a.AssetType)
            .ToListAsync();
    }

    public async Task<IEnumerable<OnboardingAsset>> GetByStatusAsync(OnboardingAssetProvisionStatus status, Guid? planId = null)
    {
        var query = _dbSet
            .Include(a => a.OnboardingPlan).ThenInclude(p => p.Employee)
            .Include(a => a.ProvisionedBy)
            .Where(a => a.Status == status && !a.IsDeleted);

        if (planId.HasValue)
            query = query.Where(a => a.OnboardingPlanId == planId.Value);

        return await query
            .OrderBy(a => a.AssetType)
            .ToListAsync();
    }
}

#endregion
