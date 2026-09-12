using ErpSystem.Core.DTOs.Workflow;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Interfaces;

/// <summary>
/// Shared helper for integrating workflow outcomes into module logic
/// </summary>
public interface IWorkflowIntegrationService
{
    /// <summary>
    /// Returns true only when approval is enabled by an active Published
    /// workflow for the current tenant and entity type.
    /// </summary>
    Task<bool> HasActiveApprovalWorkflowAsync(string entityType);
    Task<bool> HasActiveApprovalInstanceAsync(string entityType, Guid entityId);

    /// <summary>
    /// Starts the approval workflow and returns a normalized outcome
    /// </summary>
    Task<WorkflowIntegrationResult> SubmitAsync(string entityType, Guid entityId);

    /// <summary>
    /// Starts the approval workflow using one exact policy-selected Published definition version.
    /// </summary>
    Task<WorkflowIntegrationResult> SubmitAsync(string entityType, Guid entityId, Guid workflowDefinitionId);

    /// <summary>
    /// Processes an approval action and returns a normalized outcome.
    /// userId must be the ApplicationUser.Id from the authenticated user, not an Employee.Id.
    /// </summary>
    Task<WorkflowIntegrationResult> ProcessApprovalAsync(string entityType, Guid entityId, Guid userId, string action,
        string? comments = null);

    /// <summary>
    /// Checks if a user can approve the current workflow step.
    /// userId must be the ApplicationUser.Id from the authenticated user, not an Employee.Id.
    /// </summary>
    Task<bool> CanUserApproveAsync(string entityType, Guid entityId, Guid userId);

    /// <summary>
    /// Cancels an active workflow instance
    /// </summary>
    Task<WorkflowExecutionResult> CancelWorkflowAsync(string entityType, Guid entityId, string reason);

    /// <summary>
    /// Recalls an active workflow instance back to the requester-owned draft state.
    /// userId must be the ApplicationUser.Id from the authenticated user, not an Employee.Id.
    /// </summary>
    Task<WorkflowIntegrationResult> RecallAsync(string entityType, Guid entityId, Guid userId, string? reason = null);
}

/// <summary>
/// Result wrapper for workflow integration operations
/// </summary>
public class WorkflowIntegrationResult
{
    public WorkflowIntegrationResult(
        WorkflowExecutionResult executionResult,
        WorkflowOutcome outcome,
        bool approvalRequired = true)
    {
        ExecutionResult = executionResult;
        Outcome = outcome;
        ApprovalRequired = approvalRequired;
    }

    public WorkflowExecutionResult ExecutionResult { get; }

    public WorkflowOutcome Outcome { get; }

    /// <summary>
    /// False when the current tenant has no active Published workflow for the
    /// entity type and the module may complete its normal direct lifecycle.
    /// </summary>
    public bool ApprovalRequired { get; }
}
