using ErpSystem.Core.Entities.Workflow;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces.Repositories;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Data.Repositories;

public sealed class WorkflowApprovalPolicySetRepository
    : GenericRepository<WorkflowApprovalPolicySet>, IWorkflowApprovalPolicySetRepository
{
    public WorkflowApprovalPolicySetRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IReadOnlyList<WorkflowApprovalPolicySet>> GetEffectiveCandidatesAsync(
        Guid tenantId,
        string entityType,
        DateTime effectiveAt,
        CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(policy => policy.TenantId == tenantId &&
                !policy.IsDeleted && policy.IsActive &&
                policy.LifecycleStatus == WorkflowDefinitionLifecycleStatus.Published &&
                policy.EffectiveFrom <= effectiveAt &&
                (!policy.EffectiveTo.HasValue || policy.EffectiveTo.Value >= effectiveAt) &&
                (policy.EntityType == null || policy.EntityType == "" || policy.EntityType == entityType))
            .ToListAsync(cancellationToken);
    }
}
