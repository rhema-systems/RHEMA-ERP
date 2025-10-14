using Microsoft.EntityFrameworkCore;
using ErpSystem.Core.Entities.Workflow;
using ErpSystem.Core.Interfaces.Repositories;
using ErpSystem.Infrastructure.Data;

namespace ErpSystem.Infrastructure.Repositories.Workflow;

public class WorkflowTransitionRepository : Repository<WorkflowTransition>, IWorkflowTransitionRepository
{
    public WorkflowTransitionRepository(ApplicationDbContext context) : base(context)
    {
    }

    public async Task<IEnumerable<WorkflowTransition>> GetFromStepAsync(Guid fromStepId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(wt => wt.FromStepId == fromStepId)
            .Include(wt => wt.FromStep)
            .Include(wt => wt.ToStep)
            .OrderBy(wt => wt.Priority)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<WorkflowTransition>> GetByWorkflowDefinitionAsync(Guid workflowDefinitionId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(wt => wt.WorkflowDefinitionId == workflowDefinitionId)
            .Include(wt => wt.FromStep)
            .Include(wt => wt.ToStep)
            .OrderBy(wt => wt.FromStep.StepOrder)
            .ThenBy(wt => wt.Priority)
            .ToListAsync(cancellationToken);
    }
}