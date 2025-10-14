using Microsoft.EntityFrameworkCore;
using ErpSystem.Core.Interfaces.Repositories;
using ErpSystem.Core.Entities.Workflow;
using ErpSystem.Core.Enums;

namespace ErpSystem.Data.Repositories;

/// <summary>
/// Repository implementation for workflow step instances using Entity Framework
/// </summary>
public class WorkflowStepInstanceRepository : GenericRepository<WorkflowStepInstance>, IWorkflowStepInstanceRepository
{
    public WorkflowStepInstanceRepository(ApplicationDbContext context) : base(context)
    {
    }

    /// <summary>
    /// Gets step instances for a workflow instance
    /// </summary>
    public async Task<IEnumerable<WorkflowStepInstance>> GetByWorkflowInstanceAsync(Guid workflowInstanceId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Include(si => si.WorkflowStep)
            .Where(si => si.WorkflowInstanceId == workflowInstanceId && !si.IsDeleted)
            .OrderBy(si => si.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Gets active step instances assigned to a user
    /// </summary>
    public async Task<IEnumerable<WorkflowStepInstance>> GetActiveAssignedToUserAsync(Guid userId, Guid tenantId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Include(si => si.WorkflowStep)
            .Include(si => si.WorkflowInstance)
            .Where(si => si.AssignedToId == userId && si.TenantId == tenantId &&
                        si.Status == WorkflowStepInstanceStatus.Pending && !si.IsDeleted)
            .OrderBy(si => si.DueDate)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Gets step instances by status
    /// </summary>
    public async Task<IEnumerable<WorkflowStepInstance>> GetByStatusAsync(WorkflowStepInstanceStatus status, Guid tenantId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Include(si => si.WorkflowStep)
            .Include(si => si.WorkflowInstance)
            .Where(si => si.Status == status && si.TenantId == tenantId && !si.IsDeleted)
            .OrderByDescending(si => si.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Gets the current active step instance for a workflow instance
    /// </summary>
    public async Task<WorkflowStepInstance?> GetCurrentStepAsync(Guid workflowInstanceId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Include(si => si.WorkflowStep)
            .Where(si => si.WorkflowInstanceId == workflowInstanceId &&
                        si.Status == WorkflowStepInstanceStatus.Pending && !si.IsDeleted)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <summary>
    /// Gets overdue step instances
    /// </summary>
    public async Task<IEnumerable<WorkflowStepInstance>> GetOverdueStepInstancesAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var currentTime = DateTime.UtcNow;
        return await _dbSet
            .Include(si => si.WorkflowStep)
            .Include(si => si.WorkflowInstance)
            .Where(si => si.TenantId == tenantId &&
                        si.Status == WorkflowStepInstanceStatus.Pending &&
                        si.DueDate.HasValue && si.DueDate < currentTime && !si.IsDeleted)
            .OrderBy(si => si.DueDate)
            .ToListAsync(cancellationToken);
    }
}
