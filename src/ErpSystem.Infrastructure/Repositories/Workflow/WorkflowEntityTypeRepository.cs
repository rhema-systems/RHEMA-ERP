using Microsoft.EntityFrameworkCore;
using ErpSystem.Core.Entities.Workflow;
using ErpSystem.Core.Interfaces.Repositories;
using ErpSystem.Infrastructure.Data;

namespace ErpSystem.Infrastructure.Repositories.Workflow;

public class WorkflowEntityTypeRepository : Repository<WorkflowEntityType>, IWorkflowEntityTypeRepository
{
    public WorkflowEntityTypeRepository(ApplicationDbContext context) : base(context)
    {
    }

    public async Task<WorkflowEntityType?> GetByNameAsync(string name, Guid tenantId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(et => et.Name == name && et.TenantId == tenantId)
            .Include(et => et.WorkflowDefinitions.Where(wd => wd.IsActive))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IEnumerable<WorkflowEntityType>> GetActiveEntityTypesAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(et => et.IsActive && et.TenantId == tenantId)
            .Include(et => et.WorkflowDefinitions.Where(wd => wd.IsActive))
            .OrderBy(et => et.DisplayName)
            .ToListAsync(cancellationToken);
    }
}