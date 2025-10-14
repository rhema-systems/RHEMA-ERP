using Microsoft.EntityFrameworkCore;
using ErpSystem.Core.Entities.Workflow;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces.Repositories;
using ErpSystem.Infrastructure.Data;

namespace ErpSystem.Infrastructure.Repositories.Workflow;

public class WorkflowInstanceRepository : Repository<WorkflowInstance>, IWorkflowInstanceRepository
{
    public WorkflowInstanceRepository(ApplicationDbContext context) : base(context)
    {
    }

    public async Task<IEnumerable<WorkflowInstance>> GetByEntityAsync(Guid entityTypeId, string entityId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(wi => wi.EntityTypeId == entityTypeId && wi.EntityId == entityId)
            .Include(wi => wi.WorkflowDefinition)
            .Include(wi => wi.EntityType)
            .Include(wi => wi.StartedBy)
            .OrderByDescending(wi => wi.StartedDate)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<WorkflowInstance>> GetActiveAssignedToUserAsync(Guid userId, Guid tenantId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(wi => wi.TenantId == tenantId && 
                        (wi.Status == WorkflowInstanceStatus.InProgress || wi.Status == WorkflowInstanceStatus.Waiting) &&
                        wi.StepInstances.Any(si => si.AssignedToId == userId && 
                                                  si.Status == WorkflowStepInstanceStatus.Pending))
            .Include(wi => wi.WorkflowDefinition)
            .Include(wi => wi.EntityType)
            .Include(wi => wi.StepInstances.Where(si => si.AssignedToId == userId && si.Status == WorkflowStepInstanceStatus.Pending))
                .ThenInclude(si => si.Step)
            .OrderBy(wi => wi.DueDate)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<WorkflowInstance>> GetByStatusAsync(WorkflowInstanceStatus status, Guid tenantId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(wi => wi.Status == status && wi.TenantId == tenantId)
            .Include(wi => wi.WorkflowDefinition)
            .Include(wi => wi.EntityType)
            .Include(wi => wi.StartedBy)
            .OrderByDescending(wi => wi.StartedDate)
            .ToListAsync(cancellationToken);
    }

    public async Task<WorkflowInstance?> GetWithDetailsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(wi => wi.Id == id)
            .Include(wi => wi.WorkflowDefinition)
                .ThenInclude(wd => wd.Steps)
                    .ThenInclude(s => s.OutgoingTransitions)
            .Include(wi => wi.EntityType)
            .Include(wi => wi.StartedBy)
            .Include(wi => wi.StepInstances)
                .ThenInclude(si => si.Step)
            .Include(wi => wi.StepInstances)
                .ThenInclude(si => si.AssignedTo)
            .Include(wi => wi.StepInstances)
                .ThenInclude(si => si.Approvals)
                    .ThenInclude(a => a.ApprovedBy)
            .Include(wi => wi.ActivityLogs)
                .ThenInclude(al => al.PerformedBy)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IEnumerable<WorkflowInstance>> GetOverdueInstancesAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        return await _dbSet
            .Where(wi => wi.TenantId == tenantId &&
                        wi.DueDate.HasValue &&
                        wi.DueDate.Value < now &&
                        (wi.Status == WorkflowInstanceStatus.InProgress || wi.Status == WorkflowInstanceStatus.Waiting))
            .Include(wi => wi.WorkflowDefinition)
            .Include(wi => wi.EntityType)
            .Include(wi => wi.StartedBy)
            .OrderBy(wi => wi.DueDate)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<WorkflowInstance>> GetStartedByUserAsync(Guid userId, Guid tenantId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(wi => wi.StartedById == userId && wi.TenantId == tenantId)
            .Include(wi => wi.WorkflowDefinition)
            .Include(wi => wi.EntityType)
            .OrderByDescending(wi => wi.StartedDate)
            .ToListAsync(cancellationToken);
    }
}