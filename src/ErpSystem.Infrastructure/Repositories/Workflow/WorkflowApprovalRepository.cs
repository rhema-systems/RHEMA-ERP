using Microsoft.EntityFrameworkCore;
using ErpSystem.Core.Entities.Workflow;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces.Repositories;
using ErpSystem.Infrastructure.Data;

namespace ErpSystem.Infrastructure.Repositories.Workflow;

public class WorkflowApprovalRepository : Repository<WorkflowApproval>, IWorkflowApprovalRepository
{
    public WorkflowApprovalRepository(ApplicationDbContext context) : base(context)
    {
    }

    public async Task<IEnumerable<WorkflowApproval>> GetByStepInstanceAsync(Guid stepInstanceId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(wa => wa.StepInstanceId == stepInstanceId)
            .Include(wa => wa.ApprovedBy)
            .Include(wa => wa.StepInstance)
                .ThenInclude(si => si.Step)
            .Include(wa => wa.StepInstance)
                .ThenInclude(si => si.WorkflowInstance)
                    .ThenInclude(wi => wi.WorkflowDefinition)
            .OrderByDescending(wa => wa.RequestedDate)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<WorkflowApproval>> GetPendingForUserAsync(Guid userId, Guid tenantId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(wa => wa.ApprovedById == userId && 
                        wa.TenantId == tenantId &&
                        wa.Status == WorkflowApprovalStatus.Pending)
            .Include(wa => wa.StepInstance)
                .ThenInclude(si => si.Step)
            .Include(wa => wa.StepInstance)
                .ThenInclude(si => si.WorkflowInstance)
                    .ThenInclude(wi => wi.WorkflowDefinition)
            .Include(wa => wa.StepInstance)
                .ThenInclude(si => si.WorkflowInstance)
                    .ThenInclude(wi => wi.EntityType)
            .OrderBy(wa => wa.DueDate)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<WorkflowApproval>> GetByStatusAsync(WorkflowApprovalStatus status, Guid tenantId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(wa => wa.Status == status && wa.TenantId == tenantId)
            .Include(wa => wa.ApprovedBy)
            .Include(wa => wa.StepInstance)
                .ThenInclude(si => si.Step)
            .Include(wa => wa.StepInstance)
                .ThenInclude(si => si.WorkflowInstance)
                    .ThenInclude(wi => wi.WorkflowDefinition)
            .OrderByDescending(wa => wa.RequestedDate)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<WorkflowApproval>> GetOverdueApprovalsAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        return await _dbSet
            .Where(wa => wa.TenantId == tenantId &&
                        wa.DueDate.HasValue &&
                        wa.DueDate.Value < now &&
                        wa.Status == WorkflowApprovalStatus.Pending)
            .Include(wa => wa.ApprovedBy)
            .Include(wa => wa.StepInstance)
                .ThenInclude(si => si.Step)
            .Include(wa => wa.StepInstance)
                .ThenInclude(si => si.WorkflowInstance)
                    .ThenInclude(wi => wi.WorkflowDefinition)
            .OrderBy(wa => wa.DueDate)
            .ToListAsync(cancellationToken);
    }
}