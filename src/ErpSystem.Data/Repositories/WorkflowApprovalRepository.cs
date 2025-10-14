using Microsoft.EntityFrameworkCore;
using ErpSystem.Core.Interfaces.Repositories;
using ErpSystem.Core.Entities.Workflow;
using ErpSystem.Core.Enums;

namespace ErpSystem.Data.Repositories;

public class WorkflowApprovalRepository : GenericRepository<WorkflowApproval>, IWorkflowApprovalRepository
{
    public WorkflowApprovalRepository(ApplicationDbContext context) : base(context)
    {
    }

    /// <summary>
    /// Gets approvals for a step instance
    /// </summary>
    public async Task<IEnumerable<WorkflowApproval>> GetByStepInstanceAsync(Guid stepInstanceId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(a => a.StepInstanceId == stepInstanceId && !a.IsDeleted)
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Gets pending approvals assigned to a user
    /// </summary>
    public async Task<IEnumerable<WorkflowApproval>> GetPendingForUserAsync(Guid userId, Guid tenantId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Include(a => a.StepInstance)
            .ThenInclude(si => si.WorkflowInstance)
            .Where(a => a.ApproverId == userId && a.TenantId == tenantId && 
                       a.Status == WorkflowApprovalStatus.Pending && !a.IsDeleted)
            .OrderBy(a => a.DueDate)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Gets approvals by status
    /// </summary>
    public async Task<IEnumerable<WorkflowApproval>> GetByStatusAsync(WorkflowApprovalStatus status, Guid tenantId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Include(a => a.StepInstance)
            .Where(a => a.Status == status && a.TenantId == tenantId && !a.IsDeleted)
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Gets overdue approvals
    /// </summary>
    public async Task<IEnumerable<WorkflowApproval>> GetOverdueApprovalsAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var currentTime = DateTime.UtcNow;
        return await _dbSet
            .Include(a => a.StepInstance)
            .Where(a => a.TenantId == tenantId && a.Status == WorkflowApprovalStatus.Pending &&
                       a.DueDate.HasValue && a.DueDate < currentTime && !a.IsDeleted)
            .OrderBy(a => a.DueDate)
            .ToListAsync(cancellationToken);
    }
}
