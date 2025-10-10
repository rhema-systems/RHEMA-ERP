using ErpSystem.Core.Entities.Workflow;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Interfaces.Repositories;

/// <summary>
/// Repository interface for workflow activity logs with activity type and date queries
/// </summary>
public interface IWorkflowActivityLogRepository
{
    // Basic CRUD operations
    Task<WorkflowActivityLog?> GetByIdAsync(Guid id);
    Task<IEnumerable<WorkflowActivityLog>> GetAllAsync();
    Task<WorkflowActivityLog> CreateAsync(WorkflowActivityLog activityLog);
    Task<WorkflowActivityLog> UpdateAsync(WorkflowActivityLog activityLog);
    Task DeleteAsync(Guid id);
    
    // Workflow instance based queries
    Task<IEnumerable<WorkflowActivityLog>> GetByWorkflowInstanceAsync(Guid workflowInstanceId);
    Task<IEnumerable<WorkflowActivityLog>> GetByWorkflowInstanceAsync(Guid workflowInstanceId, DateTime? fromDate = null);
    Task<WorkflowActivityLog?> GetLatestActivityAsync(Guid workflowInstanceId);
    
    // Step instance based queries
    Task<IEnumerable<WorkflowActivityLog>> GetByStepInstanceAsync(Guid stepInstanceId);
    Task<WorkflowActivityLog?> GetLatestStepActivityAsync(Guid stepInstanceId);
    
    // Activity type based queries
    Task<IEnumerable<WorkflowActivityLog>> GetByActivityTypeAsync(WorkflowActivityType activityType);
    Task<IEnumerable<WorkflowActivityLog>> GetByActivityTypeAsync(string activityType);
    Task<IEnumerable<WorkflowActivityLog>> GetByActivityTypesAsync(IEnumerable<WorkflowActivityType> activityTypes);
    
    // User based queries
    Task<IEnumerable<WorkflowActivityLog>> GetByUserAsync(Guid userId);
    Task<IEnumerable<WorkflowActivityLog>> GetByUserAsync(Guid userId, DateTime? fromDate = null);
    Task<IEnumerable<WorkflowActivityLog>> GetUserActivityInWorkflowAsync(Guid userId, Guid workflowInstanceId);
    
    // Date based queries
    Task<IEnumerable<WorkflowActivityLog>> GetBetweenDatesAsync(DateTime startDate, DateTime endDate);
    Task<IEnumerable<WorkflowActivityLog>> GetRecentActivityAsync(int hours = 24);
    Task<IEnumerable<WorkflowActivityLog>> GetTodayActivityAsync();
    
    // Activity audit and tracking
    Task<IEnumerable<WorkflowActivityLog>> GetWorkflowTimelineAsync(Guid workflowInstanceId);
    Task<IEnumerable<WorkflowActivityLog>> GetStepTimelineAsync(Guid stepInstanceId);
    Task<IEnumerable<WorkflowActivityLog>> GetUserActivityTimelineAsync(Guid userId, DateTime? fromDate = null);
    
    // Error and issue tracking
    Task<IEnumerable<WorkflowActivityLog>> GetErrorLogsAsync(DateTime? fromDate = null);
    Task<IEnumerable<WorkflowActivityLog>> GetFailureLogsAsync(Guid? workflowInstanceId = null);
    
    // Statistics and reporting
    Task<Dictionary<string, int>> GetActivityTypeCountsAsync(DateTime? fromDate = null);
    Task<Dictionary<Guid, int>> GetUserActivityCountsAsync(DateTime? fromDate = null);
    Task<Dictionary<DateTime, int>> GetDailyActivityCountsAsync(DateTime fromDate, DateTime toDate);
    Task<int> GetActivityCountAsync(Guid workflowInstanceId);
    
    // Search and filtering
    Task<IEnumerable<WorkflowActivityLog>> SearchByDescriptionAsync(string searchTerm);
    Task<IEnumerable<WorkflowActivityLog>> SearchByDataAsync(string searchTerm);
    Task<IEnumerable<WorkflowActivityLog>> GetPagedAsync(int page, int pageSize, Guid? workflowInstanceId = null, string? activityType = null);
    
    // Cleanup and maintenance
    Task<int> DeleteOldLogsAsync(DateTime beforeDate);
    Task<int> GetLogCountAsync(DateTime? fromDate = null);
    Task<DateTime?> GetOldestLogDateAsync();
    Task<DateTime?> GetNewestLogDateAsync();
    
    Task SaveChangesAsync();
}