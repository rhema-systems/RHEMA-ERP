using Microsoft.EntityFrameworkCore;
using ErpSystem.Core.Interfaces.Repositories;
using ErpSystem.Core.Entities.Workflow;
using ErpSystem.Core.Enums;

namespace ErpSystem.Data.Repositories;

/// <summary>
/// Repository implementation for workflow instances using Entity Framework
/// </summary>
public class WorkflowInstanceRepository : GenericRepository<WorkflowInstance>, IWorkflowInstanceRepository
{
    public WorkflowInstanceRepository(ApplicationDbContext context) : base(context)
    {
    }
    #region Interface Implementation

    /// <summary>
    /// Gets workflow instances by entity
    /// </summary>
    public async Task<IEnumerable<WorkflowInstance>> GetByEntityAsync(Guid entityTypeId, string entityId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Include(wi => wi.WorkflowDefinition)
            .Where(wi => wi.WorkflowDefinition.EntityTypeId == entityTypeId && wi.EntityId.ToString() == entityId && !wi.IsDeleted)
            .OrderByDescending(wi => wi.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Gets active workflow instances assigned to a user
    /// </summary>
    public async Task<IEnumerable<WorkflowInstance>> GetActiveAssignedToUserAsync(Guid userId, Guid tenantId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Include(wi => wi.WorkflowDefinition)
            .Include(wi => wi.StepInstances)
            .Where(wi => wi.TenantId == tenantId && 
                        wi.Status == WorkflowInstanceStatus.InProgress &&
                        wi.StepInstances.Any(si => si.AssignedToId == userId && 
                                                  si.Status == WorkflowStepInstanceStatus.Pending) &&
                        !wi.IsDeleted)
            .OrderByDescending(wi => wi.UpdatedAt)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Gets workflow instances by status
    /// </summary>
    public async Task<IEnumerable<WorkflowInstance>> GetByStatusAsync(WorkflowInstanceStatus status, Guid tenantId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Include(wi => wi.WorkflowDefinition)
            .Where(wi => wi.TenantId == tenantId && wi.Status == status && !wi.IsDeleted)
            .OrderByDescending(wi => wi.UpdatedAt)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Gets workflow instance with all related data
    /// </summary>
    public async Task<WorkflowInstance?> GetWithDetailsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Include(wi => wi.WorkflowDefinition)
            .ThenInclude(wd => wd.Steps)
            .Include(wi => wi.StepInstances)
            .ThenInclude(si => si.WorkflowStep)
            .Include(wi => wi.ActivityLogs)
            .FirstOrDefaultAsync(wi => wi.Id == id && !wi.IsDeleted, cancellationToken);
    }

    /// <summary>
    /// Gets overdue workflow instances
    /// </summary>
    public async Task<IEnumerable<WorkflowInstance>> GetOverdueInstancesAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var currentTime = DateTime.UtcNow;
        return await _dbSet
            .Include(wi => wi.WorkflowDefinition)
            .Include(wi => wi.StepInstances)
            .Where(wi => wi.TenantId == tenantId && 
                        wi.Status == WorkflowInstanceStatus.InProgress &&
                        !wi.IsDeleted)
            .OrderBy(wi => wi.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Gets workflow instances started by a specific user
    /// </summary>
    public async Task<IEnumerable<WorkflowInstance>> GetStartedByUserAsync(Guid userId, Guid tenantId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Include(wi => wi.WorkflowDefinition)
            .Where(wi => wi.TenantId == tenantId && wi.StartedById == userId && !wi.IsDeleted)
            .OrderByDescending(wi => wi.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    #endregion
}