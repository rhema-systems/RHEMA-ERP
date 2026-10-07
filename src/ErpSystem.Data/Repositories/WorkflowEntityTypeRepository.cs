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
        var exact = await _dbSet
            .FirstOrDefaultAsync(et => et.Name == name && et.TenantId == tenantId && !et.IsDeleted, cancellationToken);
        if (exact is not null) return exact;

        // Runtime services normally pass the configured entity code back into this
        // repository after a page-level alias has been resolved. Prefer that exact
        // code before the normalized fallback below. Older tenants can contain both
        // a legacy SalesOrder row and the catalog SALES_ORDER row; treating those as
        // ambiguous makes otherwise valid entity-summary reads fail with HTTP 500.
        exact = await _dbSet
            .FirstOrDefaultAsync(et => et.Code == name && et.TenantId == tenantId && !et.IsDeleted, cancellationToken);
        if (exact is not null) return exact;

        // An entity type may be disabled after an approval has started. Resolve
        // its code/name alias without filtering IsActive so that its in-flight
        // records do not appear to have no workflow. New submissions separately
        // require an active type and Published definition.
        static string Key(string value) => new(value.Where(char.IsLetterOrDigit).Select(char.ToUpperInvariant).ToArray());
        var key = Key(name);
        var candidates = await _dbSet.Where(et => et.TenantId == tenantId && !et.IsDeleted)
            .ToListAsync(cancellationToken);
        var matches = candidates.Where(et => Key(et.Code) == key || Key(et.Name) == key).ToList();
        if (matches.Count > 1)
            throw new InvalidOperationException("Multiple workflow entity types match this record. Resolve the configuration before continuing.");
        return matches.SingleOrDefault();
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
