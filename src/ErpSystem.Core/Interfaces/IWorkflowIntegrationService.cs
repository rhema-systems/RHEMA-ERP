using ErpSystem.Core.DTOs.Workflow;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Interfaces;

/// <summary>
/// Shared helper for integrating workflow outcomes into module logic
/// </summary>
public interface IWorkflowIntegrationService
{
    /// <summary>
    /// Starts the approval workflow and returns a normalized outcome
    /// </summary>
    Task<WorkflowIntegrationResult> SubmitAsync(string entityType, Guid entityId);

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
    public WorkflowIntegrationResult(WorkflowExecutionResult executionResult, WorkflowOutcome outcome)
    {
        ExecutionResult = executionResult;
        Outcome = outcome;
    }

    public WorkflowExecutionResult ExecutionResult { get; }

    public WorkflowOutcome Outcome { get; }
}
