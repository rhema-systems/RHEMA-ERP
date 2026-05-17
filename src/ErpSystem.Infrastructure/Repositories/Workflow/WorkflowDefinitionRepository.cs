using Microsoft.EntityFrameworkCore;
using ErpSystem.Core.Entities.Workflow;
using ErpSystem.Core.Interfaces.Repositories;
using ErpSystem.Infrastructure.Data;

namespace ErpSystem.Infrastructure.Repositories.Workflow;

public class WorkflowDefinitionRepository : Repository<WorkflowDefinition>, IWorkflowDefinitionRepository
{
    public WorkflowDefinitionRepository(ApplicationDbContext context) : base(context)
    {
    }

    public async Task<IEnumerable<WorkflowDefinition>> GetActiveByEntityTypeAsync(Guid entityTypeId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(wd => wd.EntityTypeId == entityTypeId && wd.IsActive)
            .Include(wd => wd.Steps)
                .ThenInclude(s => s.OutgoingTransitions)
            .Include(wd => wd.EntityType)
            .OrderBy(wd => wd.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<WorkflowDefinition?> GetByNameAsync(string name, Guid tenantId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(wd => wd.Name == name && wd.TenantId == tenantId)
            .Include(wd => wd.Steps)
                .ThenInclude(s => s.OutgoingTransitions)
            .Include(wd => wd.EntityType)
            .OrderByDescending(wd => wd.IsActive)
            .ThenByDescending(wd => wd.Version)
            .ThenByDescending(wd => wd.UpdatedAt ?? wd.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<WorkflowDefinition?> GetWithDetailsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(wd => wd.Id == id)
            .Include(wd => wd.Steps)
                .ThenInclude(s => s.OutgoingTransitions)
                    .ThenInclude(t => t.ToStep)
            .Include(wd => wd.Steps)
                .ThenInclude(s => s.IncomingTransitions)
                    .ThenInclude(t => t.FromStep)
            .Include(wd => wd.Instances)
            .Include(wd => wd.EntityType)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IEnumerable<WorkflowDefinition>> GetActiveDefinitionsAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(wd => wd.IsActive && wd.TenantId == tenantId)
            .Include(wd => wd.EntityType)
            .OrderBy(wd => wd.Name)
            .ToListAsync(cancellationToken);
    }
}
