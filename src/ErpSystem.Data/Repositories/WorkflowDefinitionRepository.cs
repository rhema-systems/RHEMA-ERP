using Microsoft.EntityFrameworkCore;
using ErpSystem.Core.Interfaces.Repositories;
using ErpSystem.Core.Entities.Workflow;
using ErpSystem.Core.Enums;

namespace ErpSystem.Data.Repositories;

/// <summary>
/// Repository implementation for workflow definitions using Entity Framework
/// </summary>
public class WorkflowDefinitionRepository : IWorkflowDefinitionRepository
{
    private readonly ApplicationDbContext _context;

    public WorkflowDefinitionRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    #region Basic CRUD Operations

    public async Task<WorkflowDefinition?> GetByIdAsync(Guid id)
    {
        return await _context.WorkflowDefinitions
            .FirstOrDefaultAsync(wd => wd.Id == id);
    }

    public async Task<WorkflowDefinition?> GetByIdWithStepsAsync(Guid id)
    {
        return await _context.WorkflowDefinitions
            .Include(wd => wd.Steps.OrderBy(s => s.Order))
            .ThenInclude(s => s.OutgoingTransitions.OrderBy(t => t.Priority))
            .FirstOrDefaultAsync(wd => wd.Id == id);
    }

    public async Task<WorkflowDefinition?> GetByNameAsync(string name, string entityType)
    {
        return await _context.WorkflowDefinitions
            .Include(wd => wd.Steps.OrderBy(s => s.Order))
            .ThenInclude(s => s.OutgoingTransitions.OrderBy(t => t.Priority))
            .FirstOrDefaultAsync(wd => wd.Name == name && wd.EntityType == entityType);
    }

    public async Task<IEnumerable<WorkflowDefinition>> GetAllAsync()
    {
        return await _context.WorkflowDefinitions
            .OrderBy(wd => wd.Name)
            .ThenBy(wd => wd.Version)
            .ToListAsync();
    }

    public async Task<IEnumerable<WorkflowDefinition>> GetByEntityTypeAsync(string entityType, bool activeOnly = true)
    {
        var query = _context.WorkflowDefinitions
            .Where(wd => wd.EntityType == entityType);

        if (activeOnly)
        {
            query = query.Where(wd => wd.IsActive);
        }

        return await query
            .OrderBy(wd => wd.Name)
            .ThenByDescending(wd => wd.Version)
            .ToListAsync();
    }

    public async Task<WorkflowDefinition> CreateAsync(WorkflowDefinition definition)
    {
        definition.Id = Guid.NewGuid();
        definition.CreatedDate = DateTime.UtcNow;
        
        await _context.WorkflowDefinitions.AddAsync(definition);
        return definition;
    }

    public async Task<WorkflowDefinition> UpdateAsync(WorkflowDefinition definition)
    {
        definition.LastModifiedDate = DateTime.UtcNow;
        _context.WorkflowDefinitions.Update(definition);
        return definition;
    }

    public async Task DeleteAsync(Guid id)
    {
        var definition = await GetByIdAsync(id);
        if (definition != null)
        {
            _context.WorkflowDefinitions.Remove(definition);
        }
    }

    #endregion

    #region Specific Queries

    public async Task<IEnumerable<WorkflowDefinition>> GetActiveDefinitionsAsync()
    {
        return await _context.WorkflowDefinitions
            .Where(wd => wd.IsActive)
            .OrderBy(wd => wd.EntityType)
            .ThenBy(wd => wd.Name)
            .ToListAsync();
    }

    public async Task<WorkflowDefinition?> GetLatestVersionAsync(string name, string entityType)
    {
        return await _context.WorkflowDefinitions
            .Where(wd => wd.Name == name && wd.EntityType == entityType)
            .OrderByDescending(wd => wd.Version)
            .FirstOrDefaultAsync();
    }

    public async Task<IEnumerable<WorkflowDefinition>> GetVersionsAsync(string name, string entityType)
    {
        return await _context.WorkflowDefinitions
            .Where(wd => wd.Name == name && wd.EntityType == entityType)
            .OrderByDescending(wd => wd.Version)
            .ToListAsync();
    }

    public async Task<bool> HasActiveInstancesAsync(Guid definitionId)
    {
        return await _context.WorkflowInstances
            .AnyAsync(wi => wi.WorkflowDefinitionId == definitionId && 
                           (wi.Status == "InProgress" || wi.Status == "Pending"));
    }

    public async Task<int> GetInstanceCountAsync(Guid definitionId)
    {
        return await _context.WorkflowInstances
            .CountAsync(wi => wi.WorkflowDefinitionId == definitionId);
    }

    public async Task<bool> ExistsAsync(string name, string entityType)
    {
        return await _context.WorkflowDefinitions
            .AnyAsync(wd => wd.Name == name && wd.EntityType == entityType);
    }

    #endregion

    #region Validation and Checks

    public async Task<bool> CanDeleteAsync(Guid id)
    {
        // Check if there are any workflow instances associated with this definition
        var hasInstances = await _context.WorkflowInstances
            .AnyAsync(wi => wi.WorkflowDefinitionId == id);
        
        return !hasInstances;
    }

    public async Task<IEnumerable<WorkflowDefinition>> SearchAsync(string searchTerm, string? entityType = null)
    {
        var query = _context.WorkflowDefinitions.AsQueryable();

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            query = query.Where(wd => wd.Name.Contains(searchTerm) || 
                                     (wd.Description != null && wd.Description.Contains(searchTerm)));
        }

        if (!string.IsNullOrWhiteSpace(entityType))
        {
            query = query.Where(wd => wd.EntityType == entityType);
        }

        return await query
            .OrderBy(wd => wd.Name)
            .ThenByDescending(wd => wd.Version)
            .ToListAsync();
    }

    #endregion

    public async Task SaveChangesAsync()
    {
        await _context.SaveChangesAsync();
    }
}