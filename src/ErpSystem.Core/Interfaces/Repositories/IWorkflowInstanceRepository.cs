using ErpSystem.Core.Entities.Workflow;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Interfaces.Repositories;

/// <summary>
/// Repository interface for workflow instances with status and entity-specific queries
/// </summary>
public interface IWorkflowInstanceRepository
{
    // Basic CRUD operations
    Task<WorkflowInstance?> GetByIdAsync(Guid id);
    Task<WorkflowInstance?> GetByIdWithDetailsAsync(Guid id);
    Task<IEnumerable<WorkflowInstance>> GetAllAsync();
    Task<WorkflowInstance> CreateAsync(WorkflowInstance instance);
    Task<WorkflowInstance> UpdateAsync(WorkflowInstance instance);
    Task DeleteAsync(Guid id);
    
    // Status-based queries
    Task<IEnumerable<WorkflowInstance>> GetByStatusAsync(WorkflowInstanceStatus status);
    Task<IEnumerable<WorkflowInstance>> GetByStatusAsync(string status);
    Task<IEnumerable<WorkflowInstance>> GetActiveInstancesAsync();
    Task<IEnumerable<WorkflowInstance>> GetCompletedInstancesAsync(DateTime? fromDate = null);
    Task<IEnumerable<WorkflowInstance>> GetStalledInstancesAsync(int timeoutMinutes = 60);
    
    // Entity-based queries
    Task<IEnumerable<WorkflowInstance>> GetByEntityAsync(Guid entityId);
    Task<IEnumerable<WorkflowInstance>> GetByEntityTypeAsync(string entityType);
    Task<WorkflowInstance?> GetActiveInstanceForEntityAsync(Guid entityId, string? workflowName = null);
    
    // User-based queries
    Task<IEnumerable<WorkflowInstance>> GetByInitiatorAsync(Guid userId);
    Task<IEnumerable<WorkflowInstance>> GetPendingForUserAsync(Guid userId);
    
    // Definition-based queries
    Task<IEnumerable<WorkflowInstance>> GetByDefinitionAsync(Guid definitionId);
    Task<int> GetInstanceCountForDefinitionAsync(Guid definitionId, WorkflowInstanceStatus? status = null);
    
    // Date-based queries
    Task<IEnumerable<WorkflowInstance>> GetCreatedBetweenAsync(DateTime startDate, DateTime endDate);
    Task<IEnumerable<WorkflowInstance>> GetCompletedBetweenAsync(DateTime startDate, DateTime endDate);
    
    // Statistics and reporting
    Task<Dictionary<string, int>> GetStatusCountsAsync();
    Task<Dictionary<string, int>> GetStatusCountsByDefinitionAsync(Guid definitionId);
    Task<Dictionary<string, int>> GetEntityTypeCountsAsync();
    
    // Search and pagination
    Task<IEnumerable<WorkflowInstance>> SearchAsync(string searchTerm);
    Task<IEnumerable<WorkflowInstance>> GetPagedAsync(int page, int pageSize, string? status = null);
    
    Task SaveChangesAsync();
}