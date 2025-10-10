using ErpSystem.Core.Entities.Workflow;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Interfaces.Repositories;

/// <summary>
/// Repository interface for workflow step instances with assignment and status queries
/// </summary>
public interface IWorkflowStepInstanceRepository
{
    // Basic CRUD operations
    Task<WorkflowStepInstance?> GetByIdAsync(Guid id);
    Task<WorkflowStepInstance?> GetByIdWithDetailsAsync(Guid id);
    Task<IEnumerable<WorkflowStepInstance>> GetAllAsync();
    Task<WorkflowStepInstance> CreateAsync(WorkflowStepInstance stepInstance);
    Task<WorkflowStepInstance> UpdateAsync(WorkflowStepInstance stepInstance);
    Task DeleteAsync(Guid id);
    
    // Instance-based queries
    Task<IEnumerable<WorkflowStepInstance>> GetByWorkflowInstanceAsync(Guid workflowInstanceId);
    Task<WorkflowStepInstance?> GetCurrentStepAsync(Guid workflowInstanceId);
    Task<IEnumerable<WorkflowStepInstance>> GetActiveStepsAsync(Guid workflowInstanceId);
    Task<IEnumerable<WorkflowStepInstance>> GetCompletedStepsAsync(Guid workflowInstanceId);
    
    // Status-based queries
    Task<IEnumerable<WorkflowStepInstance>> GetByStatusAsync(WorkflowStepInstanceStatus status);
    Task<IEnumerable<WorkflowStepInstance>> GetByStatusAsync(string status);
    Task<IEnumerable<WorkflowStepInstance>> GetPendingStepsAsync();
    Task<IEnumerable<WorkflowStepInstance>> GetInProgressStepsAsync();
    Task<IEnumerable<WorkflowStepInstance>> GetOverdueStepsAsync();
    
    // Assignment-based queries
    Task<IEnumerable<WorkflowStepInstance>> GetAssignedToUserAsync(Guid userId);
    Task<IEnumerable<WorkflowStepInstance>> GetAssignedToRoleAsync(string role);
    Task<IEnumerable<WorkflowStepInstance>> GetUnassignedStepsAsync();
    Task<IEnumerable<WorkflowStepInstance>> GetPendingForUserAsync(Guid userId);
    
    // Step definition queries
    Task<IEnumerable<WorkflowStepInstance>> GetByStepDefinitionAsync(Guid stepId);
    Task<WorkflowStepInstance?> GetLatestInstanceForStepAsync(Guid stepId, Guid workflowInstanceId);
    
    // Time-based queries
    Task<IEnumerable<WorkflowStepInstance>> GetCreatedBetweenAsync(DateTime startDate, DateTime endDate);
    Task<IEnumerable<WorkflowStepInstance>> GetCompletedBetweenAsync(DateTime startDate, DateTime endDate);
    Task<IEnumerable<WorkflowStepInstance>> GetDueBetweenAsync(DateTime startDate, DateTime endDate);
    
    // Statistics and reporting
    Task<Dictionary<string, int>> GetStatusCountsAsync();
    Task<Dictionary<string, int>> GetStatusCountsForWorkflowAsync(Guid workflowInstanceId);
    Task<Dictionary<Guid, int>> GetAssignmentCountsAsync();
    Task<TimeSpan?> GetAverageCompletionTimeAsync(Guid stepId);
    
    // Search and filtering
    Task<IEnumerable<WorkflowStepInstance>> SearchByCommentsAsync(string searchTerm);
    Task<IEnumerable<WorkflowStepInstance>> GetPagedAsync(int page, int pageSize, Guid? userId = null, string? status = null);
    
    Task SaveChangesAsync();
}