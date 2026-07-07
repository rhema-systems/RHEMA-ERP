using ErpSystem.Core.DTOs.Workflow;
using ErpSystem.Core.Entities.Workflow;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Interfaces.Workflow;

/// <summary>
/// Main workflow engine interface for executing and managing workflows
/// </summary>
public interface IWorkflowEngine
{
    /// <summary>
    /// Starts a new workflow instance for the specified entity
    /// </summary>
    Task<WorkflowInstance> StartWorkflowAsync(string workflowName, Guid entityId, Guid initiatedById,
        object? dataContext = null);

    /// <summary>
    /// Continues execution of a workflow instance
    /// </summary>
    Task<WorkflowExecutionResult> ExecuteNextStepAsync(Guid workflowInstanceId, Guid userId,
        object? stepData = null);

    /// <summary>
    /// Executes a specific transition from the current step (supports branching workflows).
    /// </summary>
    Task<WorkflowExecutionResult> ExecuteTransitionAsync(Guid workflowInstanceId, Guid userId, Guid transitionId,
        object? stepData = null);

    /// <summary>
    /// Executes a specific transition from the current step by name (supports branching workflows).
    /// </summary>
    Task<WorkflowExecutionResult> ExecuteTransitionAsync(Guid workflowInstanceId, Guid userId, string transitionName,
        object? stepData = null);

    /// <summary>
    /// Processes a specific step in a workflow instance
    /// </summary>
    Task<WorkflowExecutionResult> ProcessStepAsync(Guid workflowStepInstanceId, Guid userId,
        WorkflowStepAction action, object? resultData = null, string? comments = null);

    /// <summary>
    /// Cancels a running workflow instance
    /// </summary>
    Task CancelWorkflowAsync(Guid workflowInstanceId, Guid userId, string reason);

    /// <summary>
    /// Evaluates workflow conditions and determines possible transitions
    /// </summary>
    Task<IEnumerable<WorkflowTransition>> GetAvailableTransitionsAsync(Guid workflowInstanceId,
        object? dataContext = null);

    /// <summary>
    /// Gets the current status and progress of a workflow instance
    /// </summary>
    Task<WorkflowStatusDto> GetWorkflowStatusAsync(Guid workflowInstanceId);

    /// <summary>
    /// Ensures approval rows are materialized for an approval step instance (self-heals older instances
    /// where approvals were not created due to missing/mismatched configuration parsing).
    /// </summary>
    Task EnsureApprovalsForStepAsync(Guid workflowStepInstanceId, object? dataContext = null);
}

/// <summary>
/// Service for managing workflow definitions
/// </summary>
public interface IWorkflowDefinitionService
{
    /// <summary>
    /// Creates a new workflow definition
    /// </summary>
    Task<WorkflowDefinition> CreateWorkflowDefinitionAsync(CreateWorkflowDefinitionDto createDto);

    /// <summary>
    /// Updates an existing workflow definition
    /// </summary>
    Task<WorkflowDefinition> UpdateWorkflowDefinitionAsync(Guid id, UpdateWorkflowDefinitionDto updateDto);

    Task<WorkflowDefinition> CloneWorkflowDefinitionDraftAsync(Guid sourceDefinitionId, string? changeSummary, Guid createdById);

    Task<WorkflowDefinition> PublishWorkflowDefinitionAsync(Guid id, Guid publishedById);

    Task<WorkflowDefinition> RetireWorkflowDefinitionAsync(Guid id, Guid retiredById);

    Task<IReadOnlyList<WorkflowDefinition>> GetWorkflowDefinitionVersionsAsync(Guid id);

    Task<WorkflowDefinitionComparisonDto> CompareWorkflowDefinitionsAsync(Guid fromDefinitionId, Guid toDefinitionId);

    /// <summary>
    /// Gets a workflow definition by ID
    /// </summary>
    Task<WorkflowDefinition?> GetWorkflowDefinitionAsync(Guid id);

    /// <summary>
    /// Gets a workflow definition by name and entity type
    /// </summary>
    Task<WorkflowDefinition?> GetWorkflowDefinitionAsync(string name, string entityType);

    /// <summary>
    /// Gets all active workflow definitions for an entity type
    /// </summary>
    Task<IEnumerable<WorkflowDefinition>> GetWorkflowDefinitionsAsync(string entityType);

    /// <summary>
    /// Validates a workflow definition structure
    /// </summary>
    Task<WorkflowValidationResult> ValidateWorkflowDefinitionAsync(Guid workflowDefinitionId);

    /// <summary>
    /// Activates or deactivates a workflow definition
    /// </summary>
    Task SetWorkflowDefinitionActiveAsync(Guid id, bool isActive, Guid modifiedById);

    /// <summary>
    /// Deletes a workflow definition (only if no instances exist)
    /// </summary>
    Task DeleteWorkflowDefinitionAsync(Guid id);
}

/// <summary>
/// Service for managing workflow instances
/// </summary>
public interface IWorkflowInstanceService
{
    /// <summary>
    /// Gets a workflow instance by ID
    /// </summary>
    Task<WorkflowInstance?> GetWorkflowInstanceAsync(Guid id);

    /// <summary>
    /// Gets all workflow instances for a specific entity
    /// </summary>
    Task<IEnumerable<WorkflowInstance>> GetWorkflowInstancesForEntityAsync(Guid entityId);

    /// <summary>
    /// Gets workflow instances by status
    /// </summary>
    Task<IEnumerable<WorkflowInstance>> GetWorkflowInstancesByStatusAsync(string status);

    /// <summary>
    /// Gets active workflow instances assigned to a user
    /// </summary>
    Task<IEnumerable<WorkflowStepInstance>> GetPendingTasksForUserAsync(Guid userId);

    /// <summary>
    /// Gets workflow instances that require approval from a user or role
    /// </summary>
    Task<IEnumerable<WorkflowApproval>> GetPendingApprovalsAsync(Guid? userId = null, string? role = null);

    /// <summary>
    /// Updates the data context of a workflow instance
    /// </summary>
    Task UpdateWorkflowDataContextAsync(Guid workflowInstanceId, object dataContext);

    /// <summary>
    /// Adds a comment to a workflow instance
    /// </summary>
    Task AddCommentAsync(Guid workflowInstanceId, Guid userId, string comment);

    /// <summary>
    /// Gets activity log for a workflow instance
    /// </summary>
    Task<IEnumerable<WorkflowActivityLog>> GetWorkflowActivityLogAsync(Guid workflowInstanceId);
}

/// <summary>
/// Service for managing workflow steps
/// </summary>
public interface IWorkflowStepService
{
    /// <summary>
    /// Assigns a step instance to a user
    /// </summary>
    Task AssignStepAsync(Guid stepInstanceId, Guid assignedToId, Guid assignedById);

    /// <summary>
    /// Reassigns a step instance to a different user
    /// </summary>
    Task ReassignStepAsync(Guid stepInstanceId, Guid newAssignedToId, Guid reassignedById,
        string? reason = null);

    /// <summary>
    /// Escalates a step instance (e.g., due to timeout)
    /// </summary>
    Task EscalateStepAsync(Guid stepInstanceId, string escalationReason);

    /// <summary>
    /// Sets due date for a step instance
    /// </summary>
    Task SetStepDueDateAsync(Guid stepInstanceId, DateTime dueDate);

    /// <summary>
    /// Gets step instances that are overdue
    /// </summary>
    Task<IEnumerable<WorkflowStepInstance>> GetOverdueStepsAsync();
}

/// <summary>
/// Service for managing workflow approvals
/// </summary>
public interface IWorkflowApprovalService
{
    /// <summary>
    /// Creates an approval request
    /// </summary>
    Task<WorkflowApproval> CreateApprovalAsync(Guid stepInstanceId, Guid approverId,
        string? approverRole = null, DateTime? dueDate = null);

    /// <summary>
    /// Processes an approval decision
    /// </summary>
    Task<WorkflowApproval> ProcessApprovalAsync(Guid approvalId, Guid userId,
        WorkflowApprovalAction action, string? comments = null);

    /// <summary>
    /// Delegates an approval to another user
    /// </summary>
    Task<WorkflowApproval> DelegateApprovalAsync(Guid approvalId, Guid delegateToId,
        Guid delegatedById, string? reason = null);

    /// <summary>
    /// Gets approval history for a step instance
    /// </summary>
    Task<IEnumerable<WorkflowApproval>> GetApprovalHistoryAsync(Guid stepInstanceId);
}

/// <summary>
/// Service for workflow condition evaluation
/// </summary>
public interface IWorkflowConditionEvaluator
{
    /// <summary>
    /// Evaluates a condition expression against the provided data context
    /// </summary>
    Task<bool> EvaluateConditionAsync(string conditionExpression, object? dataContext);

    /// <summary>
    /// Validates a condition expression syntax
    /// </summary>
    bool ValidateConditionSyntax(string conditionExpression);

    /// <summary>
    /// Gets available variables for condition expressions based on entity type
    /// </summary>
    Task<IEnumerable<WorkflowVariableInfo>> GetAvailableVariablesAsync(string entityType);
}

/// <summary>
/// Service for workflow notification management
/// </summary>
public interface IWorkflowNotificationService
{
    /// <summary>
    /// Sends notification when a workflow is submitted/started for an entity.
    /// </summary>
    Task SendWorkflowSubmittedNotificationAsync(Guid workflowInstanceId);

    /// <summary>
    /// Sends notification when a step is assigned
    /// </summary>
    Task SendStepAssignmentNotificationAsync(Guid stepInstanceId, Guid assignedToId);

    /// <summary>
    /// Sends notification when approval is requested
    /// </summary>
    Task SendApprovalRequestNotificationAsync(Guid approvalId);

    /// <summary>
    /// Sends escalation notification
    /// </summary>
    Task SendEscalationNotificationAsync(Guid stepInstanceId, string escalationReason);

    /// <summary>
    /// Sends notification when workflow is completed
    /// </summary>
    Task SendWorkflowCompletionNotificationAsync(Guid workflowInstanceId);

    /// <summary>
    /// Sends notification when workflow is rejected/cancelled.
    /// </summary>
    Task SendWorkflowRejectedNotificationAsync(Guid workflowInstanceId, Guid rejectedById, string? comments = null);

    /// <summary>
    /// Sends overdue step reminders
    /// </summary>
    Task SendOverdueStepRemindersAsync();
}

/// <summary>
/// Resolves human-friendly entity display info for workflow-related UX (notifications, logs, etc.).
/// Centralized so modules don't have to hardcode per-entity naming.
/// </summary>
public interface IWorkflowEntityDisplayService
{
    Task<WorkflowEntityDisplayInfo> GetEntityDisplayInfoAsync(string entityType, Guid entityId);
}

public class WorkflowEntityDisplayInfo
{
    public string EntityType { get; set; } = string.Empty; // canonical type for UI navigation (e.g., "PurchaseRequisition")
    public Guid EntityId { get; set; }
    public string? EntityNumber { get; set; } // PR-2026-0002, PO-..., etc
    public string? EntityName { get; set; } // optional: supplier name, tender title, etc
    public string? ActionUrl { get; set; } // optional: explicit UI route
}
