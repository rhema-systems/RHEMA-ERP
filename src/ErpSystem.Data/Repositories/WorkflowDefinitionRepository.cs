using ErpSystem.Core.Entities.Workflow;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces.Repositories;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Data.Repositories;

/// <summary>
/// Repository implementation for workflow definitions using Entity Framework
/// </summary>
public class WorkflowDefinitionRepository : GenericRepository<WorkflowDefinition>, IWorkflowDefinitionRepository
{
    public WorkflowDefinitionRepository(ApplicationDbContext context) : base(context)
    {
    }

    /// <summary>
    /// Gets active workflow definitions for a specific entity type
    /// </summary>
    public async Task<IEnumerable<WorkflowDefinition>> GetActiveByEntityTypeAsync(Guid entityTypeId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Include(wd => wd.EntityType)
            .Where(wd => wd.EntityTypeId == entityTypeId && wd.IsActive && !wd.IsDeleted)
            .OrderBy(wd => wd.Name)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Gets a workflow definition by name
    /// </summary>
    public async Task<WorkflowDefinition?> GetByNameAsync(string name, Guid tenantId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .FirstOrDefaultAsync(wd => wd.Name == name && wd.TenantId == tenantId && !wd.IsDeleted, cancellationToken);
    }

    /// <summary>
    /// Gets workflow definitions with their steps and transitions
    /// </summary>
    public async Task<WorkflowDefinition?> GetWithDetailsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Include(wd => wd.Steps.OrderBy(s => s.Order))
            .ThenInclude(s => s.OutgoingTransitions)
            .Include(wd => wd.Steps)
            .ThenInclude(s => s.IncomingTransitions)
            .Include(wd => wd.EntityType)
            .FirstOrDefaultAsync(wd => wd.Id == id && !wd.IsDeleted, cancellationToken);
    }

    /// <summary>
    /// Gets all active workflow definitions
    /// </summary>
    public async Task<IEnumerable<WorkflowDefinition>> GetActiveDefinitionsAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Include(wd => wd.EntityType)
            .Where(wd => wd.TenantId == tenantId && wd.IsActive && !wd.IsDeleted)
            .OrderBy(wd => wd.Name)
            .ToListAsync(cancellationToken);
    }
}
