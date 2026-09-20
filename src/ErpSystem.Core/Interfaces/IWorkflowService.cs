namespace ErpSystem.Core.Interfaces;

/// <summary>
/// Service interface for workflow operations
/// </summary>
public interface IWorkflowService
{
    /// <summary>
    /// Returns true only when the current tenant has an active Published
    /// approval workflow for the requested entity type.
    /// </summary>
    Task<bool> HasActiveApprovalWorkflowAsync(string entityType);

    /// <summary>Existing approvals retain their route when a definition is deactivated.</summary>
    Task<bool> HasActiveApprovalInstanceAsync(string entityType, Guid entityId);

    /// <summary>
    /// Starts an approval workflow for an entity
    /// </summary>
    Task<ErpSystem.Core.DTOs.Workflow.WorkflowExecutionResult> StartApprovalWorkflowAsync(string entityType, Guid entityId);

    /// <summary>
    /// Starts approval using one exact Published workflow-definition version.
    /// </summary>
    Task<ErpSystem.Core.DTOs.Workflow.WorkflowExecutionResult> StartApprovalWorkflowAsync(
        string entityType, Guid entityId, Guid workflowDefinitionId);

    /// <summary>
    /// Checks if a user can approve a specific workflow step.
    /// userId must be the ApplicationUser.Id from the authenticated user, not an Employee.Id.
    /// </summary>
    Task<bool> CanUserApproveAsync(string entityType, Guid entityId, Guid userId);

    /// <summary>
    /// Processes an approval step.
    /// userId must be the ApplicationUser.Id from the authenticated user, not an Employee.Id.
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

    /// <summary>
    /// Recalls an active workflow back to the requester-owned draft state.
    /// userId must be the ApplicationUser.Id from the authenticated user, not an Employee.Id.
    /// </summary>
    Task<ErpSystem.Core.DTOs.Workflow.WorkflowExecutionResult> RecallWorkflowAsync(string entityType, Guid entityId, Guid userId, string? reason = null);
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
