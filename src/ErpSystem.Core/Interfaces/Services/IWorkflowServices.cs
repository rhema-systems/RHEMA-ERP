using ErpSystem.Core.Entities.Workflow;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Interfaces.Services;

/// <summary>
/// Service interface for workflow definition management
/// </summary>
public interface IWorkflowDefinitionService
{
    /// <summary>
    /// Creates a new workflow definition
    /// </summary>
    Task<WorkflowDefinition> CreateDefinitionAsync(WorkflowDefinition definition, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing workflow definition
    /// </summary>
    Task<WorkflowDefinition> UpdateDefinitionAsync(WorkflowDefinition definition, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a workflow definition by ID with all details
    /// </summary>
    Task<WorkflowDefinition?> GetDefinitionAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all active workflow definitions for a tenant
    /// </summary>
    Task<IEnumerable<WorkflowDefinition>> GetActiveDefinitionsAsync(Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets workflow definitions for a specific entity type
    /// </summary>
    Task<IEnumerable<WorkflowDefinition>> GetDefinitionsByEntityTypeAsync(Guid entityTypeId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Activates a workflow definition
    /// </summary>
    Task ActivateDefinitionAsync(Guid definitionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deactivates a workflow definition
    /// </summary>
    Task DeactivateDefinitionAsync(Guid definitionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates a workflow definition for consistency
    /// </summary>
    Task<(bool IsValid, IEnumerable<string> ValidationErrors)> ValidateDefinitionAsync(WorkflowDefinition definition, CancellationToken cancellationToken = default);
}

/// <summary>
/// Service interface for workflow instance management and execution
/// </summary>
public interface IWorkflowInstanceService
{
    /// <summary>
    /// Starts a new workflow instance for an entity
    /// </summary>
    Task<WorkflowInstance> StartWorkflowAsync(Guid definitionId, Guid entityTypeId, string entityId, Guid startedById, object? initialData = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a workflow instance by ID with all details
    /// </summary>
    Task<WorkflowInstance?> GetInstanceAsync(Guid instanceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets workflow instances for a specific entity
    /// </summary>
    Task<IEnumerable<WorkflowInstance>> GetInstancesByEntityAsync(Guid entityTypeId, string entityId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets active workflow instances assigned to a user
    /// </summary>
    Task<IEnumerable<WorkflowInstance>> GetActiveAssignedToUserAsync(Guid userId, Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets workflow instances started by a user
    /// </summary>
    Task<IEnumerable<WorkflowInstance>> GetInstancesStartedByUserAsync(Guid userId, Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cancels a workflow instance
    /// </summary>
    Task CancelWorkflowAsync(Guid instanceId, Guid cancelledById, string? reason = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Restarts a failed or cancelled workflow
    /// </summary>
    Task<WorkflowInstance> RestartWorkflowAsync(Guid instanceId, Guid restartedById, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates workflow instance data
    /// </summary>
    Task UpdateInstanceDataAsync(Guid instanceId, object data, Guid updatedById, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets overdue workflow instances
    /// </summary>
    Task<IEnumerable<WorkflowInstance>> GetOverdueInstancesAsync(Guid tenantId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Service interface for workflow step execution
/// </summary>
public interface IWorkflowStepService
{
    /// <summary>
    /// Completes a workflow step
    /// </summary>
    Task CompleteStepAsync(Guid stepInstanceId, Guid completedById, object? stepData = null, string? comments = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Assigns a step to a specific user
    /// </summary>
    Task AssignStepAsync(Guid stepInstanceId, Guid assignedToId, Guid assignedById, string? comments = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reassigns a step to a different user
    /// </summary>
    Task ReassignStepAsync(Guid stepInstanceId, Guid newAssignedToId, Guid reassignedById, string? reason = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Escalates a step to manager or next level
    /// </summary>
    Task EscalateStepAsync(Guid stepInstanceId, Guid escalatedById, string? reason = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets current active step for a workflow instance
    /// </summary>
    Task<WorkflowStepInstance?> GetCurrentStepAsync(Guid workflowInstanceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets active steps assigned to a user
    /// </summary>
    Task<IEnumerable<WorkflowStepInstance>> GetActiveStepsForUserAsync(Guid userId, Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets overdue steps
    /// </summary>
    Task<IEnumerable<WorkflowStepInstance>> GetOverdueStepsAsync(Guid tenantId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Service interface for workflow approval management
/// </summary>
public interface IWorkflowApprovalService
{
    /// <summary>
    /// Requests approval for a workflow step
    /// </summary>
    Task<WorkflowApproval> RequestApprovalAsync(Guid stepInstanceId, Guid approvedById, DateTime? dueDate = null, string? comments = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Processes an approval decision
    /// </summary>
    Task ProcessApprovalAsync(Guid approvalId, WorkflowApprovalAction action, Guid decidedById, string? comments = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Delegates an approval to another user
    /// </summary>
    Task DelegateApprovalAsync(Guid approvalId, Guid delegatedToId, Guid delegatedById, string? reason = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets pending approvals for a user
    /// </summary>
    Task<IEnumerable<WorkflowApproval>> GetPendingApprovalsAsync(Guid userId, Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets approvals for a specific step instance
    /// </summary>
    Task<IEnumerable<WorkflowApproval>> GetStepApprovalsAsync(Guid stepInstanceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets overdue approvals
    /// </summary>
    Task<IEnumerable<WorkflowApproval>> GetOverdueApprovalsAsync(Guid tenantId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Service interface for workflow activity logging
/// </summary>
public interface IWorkflowActivityService
{
    /// <summary>
    /// Logs a workflow activity
    /// </summary>
    Task LogActivityAsync(Guid workflowInstanceId, WorkflowActivityType activityType, string title, string? description = null, Guid? performedById = null, Guid? stepInstanceId = null, object? data = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets activity log for a workflow instance
    /// </summary>
    Task<IEnumerable<WorkflowActivityLog>> GetInstanceActivityLogAsync(Guid workflowInstanceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets recent activity logs for a tenant
    /// </summary>
    Task<IEnumerable<WorkflowActivityLog>> GetRecentActivityAsync(Guid tenantId, int count = 100, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets activity logs for a specific user
    /// </summary>
    Task<IEnumerable<WorkflowActivityLog>> GetUserActivityAsync(Guid userId, Guid tenantId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Service interface for workflow entity type management
/// </summary>
public interface IWorkflowEntityTypeService
{
    /// <summary>
    /// Creates a new workflow entity type
    /// </summary>
    Task<WorkflowEntityType> CreateEntityTypeAsync(WorkflowEntityType entityType, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing workflow entity type
    /// </summary>
    Task<WorkflowEntityType> UpdateEntityTypeAsync(WorkflowEntityType entityType, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets an entity type by ID
    /// </summary>
    Task<WorkflowEntityType?> GetEntityTypeAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets an entity type by name
    /// </summary>
    Task<WorkflowEntityType?> GetEntityTypeByNameAsync(string name, Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all active entity types for a tenant
    /// </summary>
    Task<IEnumerable<WorkflowEntityType>> GetActiveEntityTypesAsync(Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Activates an entity type
    /// </summary>
    Task ActivateEntityTypeAsync(Guid entityTypeId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deactivates an entity type
    /// </summary>
    Task DeactivateEntityTypeAsync(Guid entityTypeId, CancellationToken cancellationToken = default);
}
