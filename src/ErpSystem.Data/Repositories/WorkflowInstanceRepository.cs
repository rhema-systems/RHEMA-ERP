using Microsoft.EntityFrameworkCore;
using ErpSystem.Core.Interfaces.Repositories;
using ErpSystem.Core.Entities.Workflow;
using ErpSystem.Core.Enums;

namespace ErpSystem.Data.Repositories;

/// <summary>
/// Repository implementation for workflow instances using Entity Framework
/// </summary>
public class WorkflowInstanceRepository : IWorkflowInstanceRepository
{
    private readonly ApplicationDbContext _context;

    public WorkflowInstanceRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    #region Basic CRUD Operations

    public async Task<WorkflowInstance?> GetByIdAsync(Guid id)
    {
        return await _context.WorkflowInstances.FirstOrDefaultAsync(wi => wi.Id == id);
    }

    public async Task<WorkflowInstance?> GetByIdWithDetailsAsync(Guid id)
    {
        return await _context.WorkflowInstances
            .Include(wi => wi.WorkflowDefinition)
            .Include(wi => wi.StepInstances.OrderBy(si => si.CreatedDate))
            .ThenInclude(si => si.WorkflowStep)
            .Include(wi => wi.ActivityLogs.OrderBy(al => al.Timestamp))
            .FirstOrDefaultAsync(wi => wi.Id == id);
    }

    public async Task<IEnumerable<WorkflowInstance>> GetAllAsync()
    {
        return await _context.WorkflowInstances
            .Include(wi => wi.WorkflowDefinition)
            .OrderByDescending(wi => wi.StartedDate)
            .ToListAsync();
    }

    public async Task<WorkflowInstance> CreateAsync(WorkflowInstance instance)
    {
        instance.Id = Guid.NewGuid();
        instance.StartedDate = DateTime.UtcNow;
        instance.LastActivityDate = DateTime.UtcNow;
        
        await _context.WorkflowInstances.AddAsync(instance);
        return instance;
    }

    public async Task<WorkflowInstance> UpdateAsync(WorkflowInstance instance)
    {
        instance.LastActivityDate = DateTime.UtcNow;
        _context.WorkflowInstances.Update(instance);
        return instance;
    }

    public async Task DeleteAsync(Guid id)
    {
        var instance = await GetByIdAsync(id);
        if (instance != null)
        {
            _context.WorkflowInstances.Remove(instance);
        }
    }

    #endregion

    #region Status-based queries

    public async Task<IEnumerable<WorkflowInstance>> GetByStatusAsync(WorkflowInstanceStatus status)
    {
        return await GetByStatusAsync(status.ToString());
    }

    public async Task<IEnumerable<WorkflowInstance>> GetByStatusAsync(string status)
    {
        return await _context.WorkflowInstances
            .Include(wi => wi.WorkflowDefinition)
            .Where(wi => wi.Status == status)
            .OrderByDescending(wi => wi.LastActivityDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<WorkflowInstance>> GetActiveInstancesAsync()
    {
        return await _context.WorkflowInstances
            .Include(wi => wi.WorkflowDefinition)
            .Where(wi => wi.Status == "InProgress" || wi.Status == "Pending")
            .OrderByDescending(wi => wi.LastActivityDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<WorkflowInstance>> GetCompletedInstancesAsync(DateTime? fromDate = null)
    {
        var query = _context.WorkflowInstances
            .Where(wi => wi.Status == "Completed");

        if (fromDate.HasValue)
        {
            query = query.Where(wi => wi.CompletedDate >= fromDate.Value);
        }

        return await query
            .Include(wi => wi.WorkflowDefinition)
            .OrderByDescending(wi => wi.CompletedDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<WorkflowInstance>> GetStalledInstancesAsync(int timeoutMinutes = 60)
    {
        var cutoffTime = DateTime.UtcNow.AddMinutes(-timeoutMinutes);
        
        return await _context.WorkflowInstances
            .Include(wi => wi.WorkflowDefinition)
            .Include(wi => wi.StepInstances)
            .Where(wi => wi.Status == "InProgress" && 
                        wi.LastActivityDate < cutoffTime)
            .OrderBy(wi => wi.LastActivityDate)
            .ToListAsync();
    }

    #endregion

    #region Entity-based queries

    public async Task<IEnumerable<WorkflowInstance>> GetByEntityAsync(Guid entityId)
    {
        return await _context.WorkflowInstances
            .Include(wi => wi.WorkflowDefinition)
            .Where(wi => wi.EntityId == entityId)
            .OrderByDescending(wi => wi.StartedDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<WorkflowInstance>> GetByEntityTypeAsync(string entityType)
    {
        return await _context.WorkflowInstances
            .Include(wi => wi.WorkflowDefinition)
            .Where(wi => wi.WorkflowDefinition.EntityType == entityType)
            .OrderByDescending(wi => wi.StartedDate)
            .ToListAsync();
    }

    public async Task<WorkflowInstance?> GetActiveInstanceForEntityAsync(Guid entityId, string? workflowName = null)
    {
        var query = _context.WorkflowInstances
            .Include(wi => wi.WorkflowDefinition)
            .Where(wi => wi.EntityId == entityId && 
                        (wi.Status == "InProgress" || wi.Status == "Pending"));

        if (!string.IsNullOrWhiteSpace(workflowName))
        {
            query = query.Where(wi => wi.WorkflowDefinition.Name == workflowName);
        }

        return await query.FirstOrDefaultAsync();
    }

    #endregion

    #region User-based queries

    public async Task<IEnumerable<WorkflowInstance>> GetByInitiatorAsync(Guid userId)
    {
        return await _context.WorkflowInstances
            .Include(wi => wi.WorkflowDefinition)
            .Where(wi => wi.InitiatedById == userId)
            .OrderByDescending(wi => wi.StartedDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<WorkflowInstance>> GetPendingForUserAsync(Guid userId)
    {
        return await _context.WorkflowInstances
            .Include(wi => wi.WorkflowDefinition)
            .Include(wi => wi.StepInstances)
            .Where(wi => wi.Status == "InProgress" &&
                        wi.StepInstances.Any(si => si.AssignedToId == userId && 
                                                  (si.Status == "Pending" || si.Status == "InProgress")))
            .OrderByDescending(wi => wi.LastActivityDate)
            .ToListAsync();
    }

    #endregion

    #region Definition-based queries

    public async Task<IEnumerable<WorkflowInstance>> GetByDefinitionAsync(Guid definitionId)
    {
        return await _context.WorkflowInstances
            .Where(wi => wi.WorkflowDefinitionId == definitionId)
            .OrderByDescending(wi => wi.StartedDate)
            .ToListAsync();
    }

    public async Task<int> GetInstanceCountForDefinitionAsync(Guid definitionId, WorkflowInstanceStatus? status = null)
    {
        var query = _context.WorkflowInstances
            .Where(wi => wi.WorkflowDefinitionId == definitionId);

        if (status.HasValue)
        {
            query = query.Where(wi => wi.Status == status.ToString());
        }

        return await query.CountAsync();
    }

    #endregion

    #region Date-based queries

    public async Task<IEnumerable<WorkflowInstance>> GetCreatedBetweenAsync(DateTime startDate, DateTime endDate)
    {
        return await _context.WorkflowInstances
            .Include(wi => wi.WorkflowDefinition)
            .Where(wi => wi.StartedDate >= startDate && wi.StartedDate <= endDate)
            .OrderByDescending(wi => wi.StartedDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<WorkflowInstance>> GetCompletedBetweenAsync(DateTime startDate, DateTime endDate)
    {
        return await _context.WorkflowInstances
            .Include(wi => wi.WorkflowDefinition)
            .Where(wi => wi.CompletedDate >= startDate && wi.CompletedDate <= endDate)
            .OrderByDescending(wi => wi.CompletedDate)
            .ToListAsync();
    }

    #endregion

    #region Statistics and reporting

    public async Task<Dictionary<string, int>> GetStatusCountsAsync()
    {
        return await _context.WorkflowInstances
            .GroupBy(wi => wi.Status)
            .ToDictionaryAsync(g => g.Key, g => g.Count());
    }

    public async Task<Dictionary<string, int>> GetStatusCountsByDefinitionAsync(Guid definitionId)
    {
        return await _context.WorkflowInstances
            .Where(wi => wi.WorkflowDefinitionId == definitionId)
            .GroupBy(wi => wi.Status)
            .ToDictionaryAsync(g => g.Key, g => g.Count());
    }

    public async Task<Dictionary<string, int>> GetEntityTypeCountsAsync()
    {
        return await _context.WorkflowInstances
            .Include(wi => wi.WorkflowDefinition)
            .GroupBy(wi => wi.WorkflowDefinition.EntityType)
            .ToDictionaryAsync(g => g.Key, g => g.Count());
    }

    #endregion

    #region Search and pagination

    public async Task<IEnumerable<WorkflowInstance>> SearchAsync(string searchTerm)
    {
        return await _context.WorkflowInstances
            .Include(wi => wi.WorkflowDefinition)
            .Where(wi => wi.WorkflowDefinition.Name.Contains(searchTerm) ||
                        (wi.Notes != null && wi.Notes.Contains(searchTerm)))
            .OrderByDescending(wi => wi.LastActivityDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<WorkflowInstance>> GetPagedAsync(int page, int pageSize, string? status = null)
    {
        var query = _context.WorkflowInstances.Include(wi => wi.WorkflowDefinition).AsQueryable();

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(wi => wi.Status == status);
        }

        return await query
            .OrderByDescending(wi => wi.LastActivityDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
    }

    #endregion

    public async Task SaveChangesAsync()
    {
        await _context.SaveChangesAsync();
    }
}