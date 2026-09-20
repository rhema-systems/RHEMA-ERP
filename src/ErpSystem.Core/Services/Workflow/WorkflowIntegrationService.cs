using ErpSystem.Core.DTOs.Workflow;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.Workflow;

/// <summary>
/// Centralized helper for workflow integration across modules
/// </summary>
public class WorkflowIntegrationService : IWorkflowIntegrationService
{
    private readonly IWorkflowService _workflowService;
    private readonly ILogger<WorkflowIntegrationService> _logger;

    public WorkflowIntegrationService(
        IWorkflowService workflowService,
        ILogger<WorkflowIntegrationService> logger)
    {
        _workflowService = workflowService;
        _logger = logger;
    }

    public Task<bool> HasActiveApprovalWorkflowAsync(string entityType)
        => _workflowService.HasActiveApprovalWorkflowAsync(entityType);

    public Task<bool> HasActiveApprovalInstanceAsync(string entityType, Guid entityId)
        => _workflowService.HasActiveApprovalInstanceAsync(entityType, entityId);

    public async Task<WorkflowIntegrationResult> SubmitAsync(string entityType, Guid entityId)
    {
        if (!await RequiresApprovalAsync(entityType, entityId))
        {
            _logger.LogInformation(
                "Approval workflow is disabled for {EntityType}; {EntityId} will use the direct lifecycle",
                entityType,
                entityId);

            return DirectLifecycleResult();
        }

        var executionResult = await _workflowService.StartApprovalWorkflowAsync(entityType, entityId);
        return CreateResult(entityType, entityId, executionResult, "submit");
    }

    public async Task<WorkflowIntegrationResult> SubmitAsync(
        string entityType,
        Guid entityId,
        Guid workflowDefinitionId)
    {
        if (!await RequiresApprovalAsync(entityType, entityId))
        {
            return DirectLifecycleResult();
        }
        var executionResult = await _workflowService.StartApprovalWorkflowAsync(
            entityType, entityId, workflowDefinitionId);
        return CreateResult(entityType, entityId, executionResult, "submit selected definition");
    }

    private async Task<bool> RequiresApprovalAsync(string entityType, Guid entityId)
    {
        if (string.IsNullOrWhiteSpace(entityType)) throw new ArgumentException("Entity type is required.", nameof(entityType));
        if (entityId == Guid.Empty) throw new ArgumentException("Entity id is required.", nameof(entityId));
        // A retired definition stops new approvals; it does not silently approve
        // records already submitted against that definition's retained version.
        return await _workflowService.HasActiveApprovalInstanceAsync(entityType, entityId) ||
            await _workflowService.HasActiveApprovalWorkflowAsync(entityType);
    }

    private static WorkflowIntegrationResult DirectLifecycleResult() => new(
        new WorkflowExecutionResult
        {
            Success = true,
            Status = WorkflowInstanceStatus.Completed,
            Message = "No active approval workflow is configured; approval is not required."
        },
        WorkflowOutcome.Approved,
        approvalRequired: false);

    public async Task<WorkflowIntegrationResult> ProcessApprovalAsync(
        string entityType,
        Guid entityId,
        Guid userId,
        string action,
        string? comments = null)
    {
        var executionResult = await _workflowService.ProcessApprovalStepAsync(entityType, entityId, userId, action, comments);
        return CreateResult(entityType, entityId, executionResult, "process");
    }

    public Task<bool> CanUserApproveAsync(string entityType, Guid entityId, Guid userId)
        => _workflowService.CanUserApproveAsync(entityType, entityId, userId);

    public Task<WorkflowExecutionResult> CancelWorkflowAsync(string entityType, Guid entityId, string reason)
        => _workflowService.CancelWorkflowAsync(entityType, entityId, reason);

    public async Task<WorkflowIntegrationResult> RecallAsync(string entityType, Guid entityId, Guid userId, string? reason = null)
    {
        var executionResult = await _workflowService.RecallWorkflowAsync(entityType, entityId, userId, reason);
        if (!executionResult.Success)
        {
            return CreateResult(entityType, entityId, executionResult, "recall");
        }

        return new WorkflowIntegrationResult(executionResult, WorkflowOutcome.Recalled);
    }

    private WorkflowIntegrationResult CreateResult(
        string entityType,
        Guid entityId,
        WorkflowExecutionResult executionResult,
        string operation)
    {
        var outcome = MapOutcome(executionResult.Status);
        if (!executionResult.Success)
        {
            _logger.LogWarning(
                "Workflow {Operation} returned Success=false for {EntityType} {EntityId}. Status: {Status}. Message: {Message}",
                operation,
                entityType,
                entityId,
                executionResult.Status,
                executionResult.Message);
        }

        return new WorkflowIntegrationResult(executionResult, outcome);
    }

    private static WorkflowOutcome MapOutcome(WorkflowInstanceStatus status)
    {
        return status switch
        {
            WorkflowInstanceStatus.Completed => WorkflowOutcome.Approved,
            WorkflowInstanceStatus.Cancelled => WorkflowOutcome.Rejected,
            WorkflowInstanceStatus.Failed => WorkflowOutcome.Rejected,
            _ => WorkflowOutcome.Pending
        };
    }
}
