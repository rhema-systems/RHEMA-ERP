using ErpSystem.Core.Entities.Workflow;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Interfaces.Repositories;

/// <summary>
/// Repository interface for workflow definitions with specific query methods
/// </summary>
public interface IWorkflowDefinitionRepository
{
    // Basic CRUD operations
    Task<WorkflowDefinition?> GetByIdAsync(Guid id);
    Task<WorkflowDefinition?> GetByIdWithStepsAsync(Guid id);
    Task<WorkflowDefinition?> GetByNameAsync(string name, string entityType);
    Task<IEnumerable<WorkflowDefinition>> GetAllAsync();
    Task<IEnumerable<WorkflowDefinition>> GetByEntityTypeAsync(string entityType, bool activeOnly = true);
    Task<WorkflowDefinition> CreateAsync(WorkflowDefinition definition);
    Task<WorkflowDefinition> UpdateAsync(WorkflowDefinition definition);
    Task DeleteAsync(Guid id);
    
    // Specific queries
    Task<IEnumerable<WorkflowDefinition>> GetActiveDefinitionsAsync();
    Task<WorkflowDefinition?> GetLatestVersionAsync(string name, string entityType);
    Task<IEnumerable<WorkflowDefinition>> GetVersionsAsync(string name, string entityType);
    Task<bool> HasActiveInstancesAsync(Guid definitionId);
    Task<int> GetInstanceCountAsync(Guid definitionId);
    Task<bool> ExistsAsync(string name, string entityType);
    
    // Validation and checks
    Task<bool> CanDeleteAsync(Guid id);
    Task<IEnumerable<WorkflowDefinition>> SearchAsync(string searchTerm, string? entityType = null);
    
    Task SaveChangesAsync();
}