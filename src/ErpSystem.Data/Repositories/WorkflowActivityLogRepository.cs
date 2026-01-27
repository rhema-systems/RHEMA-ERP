using ErpSystem.Core.Entities.Workflow;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces.Repositories;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Data.Repositories;

public class WorkflowActivityLogRepository : GenericRepository<WorkflowActivityLog>, IWorkflowActivityLogRepository
{
    public WorkflowActivityLogRepository(ApplicationDbContext context) : base(context)
    {
    }

    /// <summary>
    /// Gets activity logs for a workflow instance
    /// </summary>
    public async Task<IEnumerable<WorkflowActivityLog>> GetByWorkflowInstanceAsync(Guid workflowInstanceId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(al => al.WorkflowInstanceId == workflowInstanceId && !al.IsDeleted)
            .OrderBy(al => al.ActivityDate)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Gets activity logs by activity type
    /// </summary>
    public async Task<IEnumerable<WorkflowActivityLog>> GetByActivityTypeAsync(WorkflowActivityType activityType, Guid tenantId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(al => al.ActivityType == activityType && al.TenantId == tenantId && !al.IsDeleted)
            .OrderByDescending(al => al.ActivityDate)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Gets recent activity logs
    /// </summary>
    public async Task<IEnumerable<WorkflowActivityLog>> GetRecentAsync(Guid tenantId, int count = 100, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(al => al.TenantId == tenantId && !al.IsDeleted)
            .OrderByDescending(al => al.ActivityDate)
            .Take(count)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Gets activity logs by user
    /// </summary>
    public async Task<IEnumerable<WorkflowActivityLog>> GetByUserAsync(Guid userId, Guid tenantId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(al => al.PerformedById == userId && al.TenantId == tenantId && !al.IsDeleted)
            .OrderByDescending(al => al.ActivityDate)
            .ToListAsync(cancellationToken);
    }
}
