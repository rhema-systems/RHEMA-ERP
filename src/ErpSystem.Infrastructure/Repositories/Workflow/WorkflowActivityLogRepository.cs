using Microsoft.EntityFrameworkCore;
using ErpSystem.Core.Entities.Workflow;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces.Repositories;
using ErpSystem.Infrastructure.Data;

namespace ErpSystem.Infrastructure.Repositories.Workflow;

public class WorkflowActivityLogRepository : Repository<WorkflowActivityLog>, IWorkflowActivityLogRepository
{
    public WorkflowActivityLogRepository(ApplicationDbContext context) : base(context)
    {
    }

    public async Task<IEnumerable<WorkflowActivityLog>> GetByWorkflowInstanceAsync(Guid workflowInstanceId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(al => al.WorkflowInstanceId == workflowInstanceId)
            .Include(al => al.PerformedBy)
            .Include(al => al.StepInstance)
                .ThenInclude(si => si.Step)
            .OrderByDescending(al => al.ActivityDate)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<WorkflowActivityLog>> GetByActivityTypeAsync(WorkflowActivityType activityType, Guid tenantId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(al => al.ActivityType == activityType && al.TenantId == tenantId)
            .Include(al => al.PerformedBy)
            .Include(al => al.WorkflowInstance)
                .ThenInclude(wi => wi.WorkflowDefinition)
            .OrderByDescending(al => al.ActivityDate)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<WorkflowActivityLog>> GetRecentAsync(Guid tenantId, int count = 100, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(al => al.TenantId == tenantId)
            .Include(al => al.PerformedBy)
            .Include(al => al.WorkflowInstance)
                .ThenInclude(wi => wi.WorkflowDefinition)
            .Include(al => al.StepInstance)
                .ThenInclude(si => si.Step)
            .OrderByDescending(al => al.ActivityDate)
            .Take(count)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<WorkflowActivityLog>> GetByUserAsync(Guid userId, Guid tenantId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(al => al.PerformedById == userId && al.TenantId == tenantId)
            .Include(al => al.WorkflowInstance)
                .ThenInclude(wi => wi.WorkflowDefinition)
            .Include(al => al.StepInstance)
                .ThenInclude(si => si.Step)
            .OrderByDescending(al => al.ActivityDate)
            .ToListAsync(cancellationToken);
    }
}