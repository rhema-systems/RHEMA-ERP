using Microsoft.EntityFrameworkCore;
using ErpSystem.Core.Entities.Workflow;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces.Repositories;
using ErpSystem.Infrastructure.Data;

namespace ErpSystem.Infrastructure.Repositories.Workflow;

public class WorkflowStepRepository : Repository<WorkflowStep>, IWorkflowStepRepository
{
    public WorkflowStepRepository(ApplicationDbContext context) : base(context)
    {
    }

    public async Task<IEnumerable<WorkflowStep>> GetByWorkflowDefinitionAsync(Guid workflowDefinitionId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(ws => ws.WorkflowDefinitionId == workflowDefinitionId)
            .Include(ws => ws.OutgoingTransitions)
            .Include(ws => ws.IncomingTransitions)
            .OrderBy(ws => ws.StepOrder)
            .ToListAsync(cancellationToken);
    }

    public async Task<WorkflowStep?> GetStartStepAsync(Guid workflowDefinitionId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(ws => ws.WorkflowDefinitionId == workflowDefinitionId && ws.IsStartStep)
            .Include(ws => ws.OutgoingTransitions)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IEnumerable<WorkflowStep>> GetByTypeAsync(WorkflowStepType stepType, Guid tenantId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(ws => ws.StepType == stepType && ws.TenantId == tenantId)
            .Include(ws => ws.WorkflowDefinition)
            .OrderBy(ws => ws.Name)
            .ToListAsync(cancellationToken);
    }
}