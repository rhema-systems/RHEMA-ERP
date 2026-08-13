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

    /// <summary>The template DTO reports TaskTemplateCount, so every read loads the task templates.</summary>
    public override async Task<OnboardingPlanTemplate?> GetByIdAsync(Guid id)
    {
        return await _dbSet
            .Include(t => t.TaskTemplates.Where(tt => !tt.IsDeleted))
            .FirstOrDefaultAsync(t => t.Id == id && !t.IsDeleted);
    }

    public override async Task<IEnumerable<OnboardingPlanTemplate>> GetAllAsync()
    {
        return await _dbSet
            .Include(t => t.TaskTemplates.Where(tt => !tt.IsDeleted))
            .Where(t => !t.IsDeleted)
            .OrderBy(t => t.Name)
            .ToListAsync();
    }

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

    // ⚠ Removed: GetByPositionIdAsync. It ignored its positionId and returned every active template.
    // OnboardingPlanTemplate has no position link — the only position on the graph is
    // OnboardingTaskTemplate.OwnerPositionId, which says who *performs* a task, not who a template is
    // *for*. The real feature is template applicability rules on the OrientationAudienceRule shape
    // (TargetType / TargetEntityId / IsInclusive), which scopes by grade, org unit and location as
    // well as position, plus a resolver with an explicit precedence rule. Tracked separately; five
    // other HR repositories expose a genuine GetByPositionIdAsync, so leaving a fake one here made
    // onboarding look like it honoured a convention it could not.
}

#endregion

// ============================================================================
// ONBOARDING TASK TEMPLATE REPOSITORY
// ============================================================================

#region Onboarding Task Template Repository

public class OnboardingTaskTemplateRepository : GenericRepository<OnboardingTaskTemplate>, IOnboardingTaskTemplateRepository
{
    public OnboardingTaskTemplateRepository(ApplicationDbContext context) : base(context) { }

    /// <summary>The task-template DTO renders PlanTemplateName and OwnerPositionTitle.</summary>
    public override async Task<OnboardingTaskTemplate?> GetByIdAsync(Guid id)
    {
        return await _dbSet
            .Include(t => t.PlanTemplate)
            .Include(t => t.OwnerPosition)
            .FirstOrDefaultAsync(t => t.Id == id && !t.IsDeleted);
    }

    public async Task<IEnumerable<OnboardingTaskTemplate>> GetByPlanTemplateIdAsync(Guid planTemplateId)
    {
        return await _dbSet
            .Include(t => t.PlanTemplate)
            .Include(t => t.OwnerPosition)
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

    /// <summary>
    /// The single include set every plan read routes through. The plan DTO renders EmployeeName /
    /// EmployeeNumber, TemplatePlanName and the three task roll-ups (total / completed / overdue);
    /// the roll-ups are counted off the Tasks collection, and an un-included collection is empty
    /// rather than null, so omitting it renders a confident "0 of 0" on a plan that has tasks.
    /// </summary>
    private IQueryable<OnboardingPlan> WithSummaryNavigations()
        => _dbSet
            .Include(p => p.Employee)
            .Include(p => p.TemplatePlan)
            .Include(p => p.Tasks.Where(t => !t.IsDeleted));

    public override async Task<OnboardingPlan?> GetByIdAsync(Guid id)
    {
        return await WithSummaryNavigations()
            .FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted);
    }

    public override async Task<IEnumerable<OnboardingPlan>> GetAllAsync()
    {
        return await WithSummaryNavigations()
            .Where(p => !p.IsDeleted)
            .OrderByDescending(p => p.StartDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<OnboardingPlan>> GetByEmployeeIdAsync(Guid employeeId)
    {
        return await WithSummaryNavigations()
            .Where(p => p.EmployeeId == employeeId && !p.IsDeleted)
            .OrderByDescending(p => p.StartDate)
            .ToListAsync();
    }

    public async Task<OnboardingPlan?> GetWithFullDetailsAsync(Guid id)
    {
        return await _dbSet
            .Include(p => p.Employee)
            .Include(p => p.TemplatePlan)
            .Include(p => p.Tasks.Where(t => !t.IsDeleted)).ThenInclude(t => t.AssignedTo)
            .Include(p => p.Tasks.Where(t => !t.IsDeleted)).ThenInclude(t => t.AssignedOrganizationUnit)
            .Include(p => p.Tasks.Where(t => !t.IsDeleted)).ThenInclude(t => t.OwnerPosition)
            .Include(p => p.Tasks.Where(t => !t.IsDeleted)).ThenInclude(t => t.CompletedBy)
            .Include(p => p.Tasks.Where(t => !t.IsDeleted)).ThenInclude(t => t.VerifiedBy)
            .Include(p => p.Tasks.Where(t => !t.IsDeleted)).ThenInclude(t => t.Comments.Where(c => !c.IsDeleted)).ThenInclude(c => c.Author)
            .Include(p => p.Assets.Where(a => !a.IsDeleted)).ThenInclude(a => a.ProvisionedBy)
            .FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted);
    }

    public async Task<IEnumerable<OnboardingPlan>> GetByStatusAsync(OnboardingStatus status)
    {
        return await WithSummaryNavigations()
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

    /// <summary>
    /// The single include set every task read routes through. The task DTO renders AssignedToName,
    /// AssignedOrganizationUnitName and VerifiedByName; before this existed each read included a
    /// different subset, so the same task showed a different set of blank columns per screen.
    /// Ordered by the author's DisplayOrder within a due date so the sequence survives.
    /// </summary>
    private IQueryable<OnboardingTask> WithSummaryNavigations()
        => _dbSet
            .Include(t => t.AssignedTo)
            .Include(t => t.AssignedOrganizationUnit)
            .Include(t => t.OwnerPosition)
            .Include(t => t.CompletedBy)
            .Include(t => t.VerifiedBy)
            .Include(t => t.OnboardingPlan).ThenInclude(p => p.Employee);

    public override async Task<OnboardingTask?> GetByIdAsync(Guid id)
    {
        return await WithSummaryNavigations()
            .FirstOrDefaultAsync(t => t.Id == id && !t.IsDeleted);
    }

    public async Task<IEnumerable<OnboardingTask>> GetByPlanIdAsync(Guid planId)
    {
        return await WithSummaryNavigations()
            .Where(t => t.OnboardingPlanId == planId && !t.IsDeleted)
            .OrderBy(t => t.DueDate).ThenBy(t => t.DisplayOrder)
            .ToListAsync();
    }

    public async Task<IEnumerable<OnboardingTask>> GetByStatusAsync(OnboardingTaskStatus status, Guid? planId = null)
    {
        var query = WithSummaryNavigations()
            .Where(t => t.Status == status && !t.IsDeleted);

        if (planId.HasValue)
            query = query.Where(t => t.OnboardingPlanId == planId.Value);

        return await query
            .OrderBy(t => t.DueDate).ThenBy(t => t.DisplayOrder)
            .ToListAsync();
    }

    public async Task<IEnumerable<OnboardingTask>> GetOverdueTasksAsync()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        return await WithSummaryNavigations()
            .Where(t => !t.IsDeleted
                     && t.Status != OnboardingTaskStatus.Completed
                     && t.DueDate < today)
            .OrderBy(t => t.DueDate).ThenBy(t => t.DisplayOrder)
            .ToListAsync();
    }

    public async Task<IEnumerable<OnboardingTask>> GetByAssignedToAsync(Guid employeeId)
    {
        return await WithSummaryNavigations()
            .Where(t => t.AssignedToId == employeeId && !t.IsDeleted)
            .OrderBy(t => t.DueDate).ThenBy(t => t.DisplayOrder)
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

    /// <summary>The comment DTO renders AuthorName.</summary>
    public override async Task<OnboardingTaskComment?> GetByIdAsync(Guid id)
    {
        return await _dbSet
            .Include(c => c.Author)
            .FirstOrDefaultAsync(c => c.Id == id && !c.IsDeleted);
    }

    public async Task<IEnumerable<OnboardingTaskComment>> GetByTaskIdAsync(Guid taskId)
    {
        // Ordered by the timestamp the DTO actually shows, not by CreatedAt — they are set in the
        // same breath today, but a thread that sorts by a field it does not display will drift.
        return await _dbSet
            .Include(c => c.Author)
            .Where(c => c.TaskId == taskId && !c.IsDeleted)
            .OrderBy(c => c.CommentDate)
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

    /// <summary>The asset DTO renders ProvisionedByName.</summary>
    public override async Task<OnboardingAsset?> GetByIdAsync(Guid id)
    {
        return await _dbSet
            .Include(a => a.ProvisionedBy)
            .FirstOrDefaultAsync(a => a.Id == id && !a.IsDeleted);
    }

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
