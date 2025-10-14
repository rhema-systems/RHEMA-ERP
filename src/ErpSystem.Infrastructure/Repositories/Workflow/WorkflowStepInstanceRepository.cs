using Microsoft.EntityFrameworkCore;
using ErpSystem.Core.Entities.Workflow;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces.Repositories;
using ErpSystem.Infrastructure.Data;

namespace ErpSystem.Infrastructure.Repositories.Workflow;

public class WorkflowStepInstanceRepository : Repository<WorkflowStepInstance>, IWorkflowStepInstanceRepository
{
    public WorkflowStepInstanceRepository(ApplicationDbContext context) : base(context)
    {
    }

    public async Task<IEnumerable<WorkflowStepInstance>> GetByWorkflowInstanceAsync(Guid workflowInstanceId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(si => si.WorkflowInstanceId == workflowInstanceId)
            .Include(si => si.Step)
            .Include(si => si.AssignedTo)
            .Include(si => si.Approvals)
                .ThenInclude(a => a.ApprovedBy)
            .OrderBy(si => si.Step.StepOrder)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<WorkflowStepInstance>> GetActiveAssignedToUserAsync(Guid userId, Guid tenantId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(si => si.AssignedToId == userId && 
                        si.TenantId == tenantId &&
                        si.Status == WorkflowStepInstanceStatus.Pending)
            .Include(si => si.Step)
            .Include(si => si.WorkflowInstance)
                .ThenInclude(wi => wi.WorkflowDefinition)
            .Include(si => si.WorkflowInstance)
                .ThenInclude(wi => wi.EntityType)
            .OrderBy(si => si.DueDate)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<WorkflowStepInstance>> GetByStatusAsync(WorkflowStepInstanceStatus status, Guid tenantId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(si => si.Status == status && si.TenantId == tenantId)
            .Include(si => si.Step)
            .Include(si => si.WorkflowInstance)
                .ThenInclude(wi => wi.WorkflowDefinition)
            .Include(si => si.AssignedTo)
            .OrderByDescending(si => si.StartedDate)
            .ToListAsync(cancellationToken);
    }

    public async Task<WorkflowStepInstance?> GetCurrentStepAsync(Guid workflowInstanceId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(si => si.WorkflowInstanceId == workflowInstanceId && 
                        (si.Status == WorkflowStepInstanceStatus.Pending || si.Status == WorkflowStepInstanceStatus.InProgress))
            .Include(si => si.Step)
            .Include(si => si.AssignedTo)
            .Include(si => si.Approvals)
                .ThenInclude(a => a.ApprovedBy)
            .OrderBy(si => si.Step.StepOrder)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IEnumerable<WorkflowStepInstance>> GetOverdueStepInstancesAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        return await _dbSet
            .Where(si => si.TenantId == tenantId &&
                        si.DueDate.HasValue &&
                        si.DueDate.Value < now &&
                        (si.Status == WorkflowStepInstanceStatus.Pending || si.Status == WorkflowStepInstanceStatus.InProgress))
            .Include(si => si.Step)
            .Include(si => si.WorkflowInstance)
                .ThenInclude(wi => wi.WorkflowDefinition)
            .Include(si => si.AssignedTo)
            .OrderBy(si => si.DueDate)
            .ToListAsync(cancellationToken);
    }
}