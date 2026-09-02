using ErpSystem.Core.Entities.Workflow;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces.Repositories;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Data.Repositories;

/// <summary>
/// Repository implementation for workflow steps using Entity Framework
/// </summary>
public class WorkflowStepRepository : GenericRepository<WorkflowStep>, IWorkflowStepRepository
{
    public WorkflowStepRepository(ApplicationDbContext context) : base(context)
    {
    }

    /// <summary>
    /// Gets steps for a specific workflow definition
    /// </summary>
    public async Task<IEnumerable<WorkflowStep>> GetByWorkflowDefinitionAsync(Guid workflowDefinitionId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Include(s => s.OutgoingTransitions)
            .Include(s => s.IncomingTransitions)
            .Where(s => s.WorkflowDefinitionId == workflowDefinitionId && !s.IsDeleted)
            // StepOrder is a display-only alias. Order by the mapped column so relational
            // providers can translate the route lookup used by workflow-start preflight.
            .OrderBy(s => s.Order)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Gets the start step for a workflow definition
    /// </summary>
    public async Task<WorkflowStep?> GetStartStepAsync(Guid workflowDefinitionId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .FirstOrDefaultAsync(s => s.WorkflowDefinitionId == workflowDefinitionId && s.IsStartStep && !s.IsDeleted, cancellationToken);
    }

    /// <summary>
    /// Gets steps by type
    /// </summary>
    public async Task<IEnumerable<WorkflowStep>> GetByTypeAsync(WorkflowStepType stepType, Guid tenantId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(s => s.StepType == stepType && s.TenantId == tenantId && !s.IsDeleted)
            .OrderBy(s => s.Name)
            .ToListAsync(cancellationToken);
    }
}
