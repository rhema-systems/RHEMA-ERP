using ErpSystem.Core.Entities.Workflow;
using ErpSystem.Core.Interfaces.Repositories;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Data.Repositories;

/// <summary>
/// Repository implementation for workflow entity types using Entity Framework
/// </summary>
public class WorkflowEntityTypeRepository : GenericRepository<WorkflowEntityType>, IWorkflowEntityTypeRepository
{
    public WorkflowEntityTypeRepository(ApplicationDbContext context) : base(context)
    {
    }

    /// <summary>
    /// Gets entity type by name
    /// </summary>
    public async Task<WorkflowEntityType?> GetByNameAsync(string name, Guid tenantId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .FirstOrDefaultAsync(et => et.Name == name && et.TenantId == tenantId && !et.IsDeleted, cancellationToken);
    }

    /// <summary>
    /// Gets all active entity types
    /// </summary>
    public async Task<IEnumerable<WorkflowEntityType>> GetActiveEntityTypesAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(et => et.TenantId == tenantId && et.IsActive && !et.IsDeleted)
            .OrderBy(et => et.Name)
            .ToListAsync(cancellationToken);
    }
}
