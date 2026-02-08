namespace ErpSystem.Core.Interfaces;

/// <summary>
/// Service interface for workflow operations
/// </summary>
public interface IWorkflowService
{
    /// <summary>
    /// Starts an approval workflow for an entity
    /// </summary>
    Task<ErpSystem.Core.DTOs.Workflow.WorkflowExecutionResult> StartApprovalWorkflowAsync(string entityType, Guid entityId);

    /// <summary>
    /// Checks if a user can approve a specific workflow step
    /// </summary>
    Task<bool> CanUserApproveAsync(string entityType, Guid entityId, Guid userId);

    /// <summary>
    /// Processes an approval step
    /// </summary>
    Task<ErpSystem.Core.DTOs.Workflow.WorkflowExecutionResult> ProcessApprovalStepAsync(string entityType, Guid entityId, Guid userId, string action, string? comments = null);

    /// <summary>
    /// Gets the current workflow step for an entity
    /// </summary>
    Task<WorkflowStepInfo?> GetCurrentWorkflowStepAsync(string entityType, Guid entityId);

    /// <summary>
    /// Gets workflow history for an entity
    /// </summary>
    Task<List<WorkflowStepInfo>> GetWorkflowHistoryAsync(string entityType, Guid entityId);

    /// <summary>
    /// Gets pending approvals for a user
    /// </summary>
    Task<List<WorkflowApprovalItem>> GetPendingApprovalsAsync(Guid userId);

    /// <summary>
    /// Cancels a workflow
    /// </summary>
    Task<ErpSystem.Core.DTOs.Workflow.WorkflowExecutionResult> CancelWorkflowAsync(string entityType, Guid entityId, string reason);
}

/// <summary>
/// Information about a workflow step
/// </summary>
public class WorkflowStepInfo
{
    public Guid Id { get; set; }
    public string StepName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public Guid? AssignedToUserId { get; set; }
    public string? AssignedToUser { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? Comments { get; set; }
    public int StepOrder { get; set; }
    public bool IsRequired { get; set; }
}

/// <summary>
/// Workflow approval item for user's pending approval list
/// </summary>
public class WorkflowApprovalItem
{
    public Guid EntityId { get; set; }
    public string EntityType { get; set; } = string.Empty;
    public string EntityTitle { get; set; } = string.Empty;
    public string EntityDescription { get; set; } = string.Empty;
    public string CurrentStep { get; set; } = string.Empty;
    public DateTime SubmittedAt { get; set; }
    public string SubmittedBy { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public int DaysPending { get; set; }
}
