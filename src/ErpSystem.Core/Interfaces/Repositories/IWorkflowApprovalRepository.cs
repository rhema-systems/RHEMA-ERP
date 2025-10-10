using ErpSystem.Core.Entities.Workflow;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Interfaces.Repositories;

/// <summary>
/// Repository interface for workflow approvals with approver and status queries
/// </summary>
public interface IWorkflowApprovalRepository
{
    // Basic CRUD operations
    Task<WorkflowApproval?> GetByIdAsync(Guid id);
    Task<WorkflowApproval?> GetByIdWithDetailsAsync(Guid id);
    Task<IEnumerable<WorkflowApproval>> GetAllAsync();
    Task<WorkflowApproval> CreateAsync(WorkflowApproval approval);
    Task<WorkflowApproval> UpdateAsync(WorkflowApproval approval);
    Task DeleteAsync(Guid id);
    
    // Step instance based queries
    Task<IEnumerable<WorkflowApproval>> GetByStepInstanceAsync(Guid stepInstanceId);
    Task<WorkflowApproval?> GetActiveApprovalForStepAsync(Guid stepInstanceId);
    
    // Status-based queries
    Task<IEnumerable<WorkflowApproval>> GetByStatusAsync(WorkflowApprovalStatus status);
    Task<IEnumerable<WorkflowApproval>> GetByStatusAsync(string status);
    Task<IEnumerable<WorkflowApproval>> GetPendingApprovalsAsync();
    Task<IEnumerable<WorkflowApproval>> GetApprovedAsync(DateTime? fromDate = null);
    Task<IEnumerable<WorkflowApproval>> GetRejectedAsync(DateTime? fromDate = null);
    
    // Approver-based queries
    Task<IEnumerable<WorkflowApproval>> GetByApproverAsync(Guid approverId);
    Task<IEnumerable<WorkflowApproval>> GetByApproverRoleAsync(string role);
    Task<IEnumerable<WorkflowApproval>> GetPendingForApproverAsync(Guid approverId);
    Task<IEnumerable<WorkflowApproval>> GetPendingForRoleAsync(string role);
    Task<IEnumerable<WorkflowApproval>> GetPendingForUserOrRoleAsync(Guid? userId, string? role);
    
    // Priority-based queries
    Task<IEnumerable<WorkflowApproval>> GetByPriorityAsync(int priority);
    Task<IEnumerable<WorkflowApproval>> GetHighPriorityPendingAsync(int maxPriority = 2);
    Task<IEnumerable<WorkflowApproval>> GetOverduePendingAsync();
    
    // Date-based queries
    Task<IEnumerable<WorkflowApproval>> GetCreatedBetweenAsync(DateTime startDate, DateTime endDate);
    Task<IEnumerable<WorkflowApproval>> GetRespondedBetweenAsync(DateTime startDate, DateTime endDate);
    Task<IEnumerable<WorkflowApproval>> GetDueBetweenAsync(DateTime startDate, DateTime endDate);
    
    // Workflow-based queries
    Task<IEnumerable<WorkflowApproval>> GetByWorkflowInstanceAsync(Guid workflowInstanceId);
    Task<IEnumerable<WorkflowApproval>> GetApprovalHistoryAsync(Guid stepInstanceId);
    
    // Statistics and reporting
    Task<Dictionary<string, int>> GetStatusCountsAsync();
    Task<Dictionary<Guid, int>> GetApproverCountsAsync();
    Task<Dictionary<string, int>> GetRoleCountsAsync();
    Task<TimeSpan?> GetAverageResponseTimeAsync();
    Task<Dictionary<int, int>> GetPriorityDistributionAsync();
    
    // Search and filtering
    Task<IEnumerable<WorkflowApproval>> SearchByCommentsAsync(string searchTerm);
    Task<IEnumerable<WorkflowApproval>> GetPagedAsync(int page, int pageSize, Guid? approverId = null, string? status = null);
    Task<IEnumerable<WorkflowApproval>> GetExpiringSoonAsync(int daysAhead = 3);
    
    Task SaveChangesAsync();
}