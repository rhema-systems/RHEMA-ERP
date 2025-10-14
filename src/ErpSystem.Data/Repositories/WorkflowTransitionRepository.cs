using Microsoft.EntityFrameworkCore;
using ErpSystem.Core.Interfaces.Repositories;
using ErpSystem.Core.Entities.Workflow;

namespace ErpSystem.Data.Repositories;

/// <summary>
/// Repository implementation for workflow transitions using Entity Framework
/// </summary>
public class WorkflowTransitionRepository : GenericRepository<WorkflowTransition>, IWorkflowTransitionRepository
{
    public WorkflowTransitionRepository(ApplicationDbContext context) : base(context)
    {
    }

    /// <summary>
    /// Gets transitions from a specific step
    /// </summary>
    public async Task<IEnumerable<WorkflowTransition>> GetFromStepAsync(Guid fromStepId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Include(t => t.FromStep)
            .Include(t => t.ToStep)
            .Where(t => t.FromStepId == fromStepId && !t.IsDeleted)
            .OrderBy(t => t.Priority)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Gets transitions for a specific workflow definition
    /// </summary>
    public async Task<IEnumerable<WorkflowTransition>> GetByWorkflowDefinitionAsync(Guid workflowDefinitionId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Include(t => t.FromStep)
            .Include(t => t.ToStep)
            .Where(t => t.FromStep.WorkflowDefinitionId == workflowDefinitionId && !t.IsDeleted)
            .OrderBy(t => t.FromStep.Order)
            .ThenBy(t => t.Priority)
            .ToListAsync(cancellationToken);
    }
}