using ErpSystem.Core.Entities.Workflow;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;

namespace ErpSystem.Core.Interfaces.Repositories;

/// <summary>
/// Repository interface for WorkflowDefinition entity
/// </summary>
public interface IWorkflowDefinitionRepository : IGenericRepository<WorkflowDefinition>
{
    /// <summary>
    /// Gets active workflow definitions for a specific entity type
    /// </summary>
    Task<IEnumerable<WorkflowDefinition>> GetActiveByEntityTypeAsync(Guid entityTypeId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a workflow definition by name
    /// </summary>
    Task<WorkflowDefinition?> GetByNameAsync(string name, Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets workflow definitions with their steps and transitions
    /// </summary>
    Task<WorkflowDefinition?> GetWithDetailsAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all active workflow definitions
    /// </summary>
    Task<IEnumerable<WorkflowDefinition>> GetActiveDefinitionsAsync(Guid tenantId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Repository interface for WorkflowStep entity
/// </summary>
public interface IWorkflowStepRepository : IGenericRepository<WorkflowStep>
{
    /// <summary>
    /// Gets steps for a specific workflow definition
    /// </summary>
    Task<IEnumerable<WorkflowStep>> GetByWorkflowDefinitionAsync(Guid workflowDefinitionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the start step for a workflow definition
    /// </summary>
    Task<WorkflowStep?> GetStartStepAsync(Guid workflowDefinitionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets steps by type
    /// </summary>
    Task<IEnumerable<WorkflowStep>> GetByTypeAsync(WorkflowStepType stepType, Guid tenantId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Repository interface for WorkflowTransition entity
/// </summary>
public interface IWorkflowTransitionRepository : IGenericRepository<WorkflowTransition>
{
    /// <summary>
    /// Gets transitions from a specific step
    /// </summary>
    Task<IEnumerable<WorkflowTransition>> GetFromStepAsync(Guid fromStepId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets transitions for a specific workflow definition
    /// </summary>
    Task<IEnumerable<WorkflowTransition>> GetByWorkflowDefinitionAsync(Guid workflowDefinitionId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Repository interface for WorkflowInstance entity
/// </summary>
public interface IWorkflowInstanceRepository : IGenericRepository<WorkflowInstance>
{
    /// <summary>
    /// Gets workflow instances by entity
    /// </summary>
    Task<IEnumerable<WorkflowInstance>> GetByEntityAsync(Guid entityTypeId, string entityId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets active workflow instances assigned to a user
    /// </summary>
    Task<IEnumerable<WorkflowInstance>> GetActiveAssignedToUserAsync(Guid userId, Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets workflow instances by status
    /// </summary>
    Task<IEnumerable<WorkflowInstance>> GetByStatusAsync(WorkflowInstanceStatus status, Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets workflow instance with all related data
    /// </summary>
    Task<WorkflowInstance?> GetWithDetailsAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets overdue workflow instances
    /// </summary>
    Task<IEnumerable<WorkflowInstance>> GetOverdueInstancesAsync(Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets workflow instances started by a specific user
    /// </summary>
    Task<IEnumerable<WorkflowInstance>> GetStartedByUserAsync(Guid userId, Guid tenantId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Repository interface for WorkflowStepInstance entity
/// </summary>
public interface IWorkflowStepInstanceRepository : IGenericRepository<WorkflowStepInstance>
{
    /// <summary>
    /// Gets step instances for a workflow instance
    /// </summary>
    Task<IEnumerable<WorkflowStepInstance>> GetByWorkflowInstanceAsync(Guid workflowInstanceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets active step instances assigned to a user
    /// </summary>
    Task<IEnumerable<WorkflowStepInstance>> GetActiveAssignedToUserAsync(Guid userId, Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets step instances by status
    /// </summary>
    Task<IEnumerable<WorkflowStepInstance>> GetByStatusAsync(WorkflowStepInstanceStatus status, Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the current active step instance for a workflow instance
    /// </summary>
    Task<WorkflowStepInstance?> GetCurrentStepAsync(Guid workflowInstanceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets overdue step instances
    /// </summary>
    Task<IEnumerable<WorkflowStepInstance>> GetOverdueStepInstancesAsync(Guid tenantId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Repository interface for WorkflowApproval entity
/// </summary>
public interface IWorkflowApprovalRepository : IGenericRepository<WorkflowApproval>
{
    /// <summary>
    /// Gets approvals for a step instance
    /// </summary>
    Task<IEnumerable<WorkflowApproval>> GetByStepInstanceAsync(Guid stepInstanceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets pending approvals assigned to a user
    /// </summary>
    Task<IEnumerable<WorkflowApproval>> GetPendingForUserAsync(Guid userId, Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets approvals by status
    /// </summary>
    Task<IEnumerable<WorkflowApproval>> GetByStatusAsync(WorkflowApprovalStatus status, Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets overdue approvals
    /// </summary>
    Task<IEnumerable<WorkflowApproval>> GetOverdueApprovalsAsync(Guid tenantId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Repository interface for WorkflowActivityLog entity
/// </summary>
public interface IWorkflowActivityLogRepository : IGenericRepository<WorkflowActivityLog>
{
    /// <summary>
    /// Gets activity logs for a workflow instance
    /// </summary>
    Task<IEnumerable<WorkflowActivityLog>> GetByWorkflowInstanceAsync(Guid workflowInstanceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets activity logs by activity type
    /// </summary>
    Task<IEnumerable<WorkflowActivityLog>> GetByActivityTypeAsync(WorkflowActivityType activityType, Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets recent activity logs
    /// </summary>
    Task<IEnumerable<WorkflowActivityLog>> GetRecentAsync(Guid tenantId, int count = 100, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets activity logs by user
    /// </summary>
    Task<IEnumerable<WorkflowActivityLog>> GetByUserAsync(Guid userId, Guid tenantId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Repository interface for WorkflowEntityType entity
/// </summary>
public interface IWorkflowEntityTypeRepository : IGenericRepository<WorkflowEntityType>
{
    /// <summary>
    /// Gets entity type by name
    /// </summary>
    Task<WorkflowEntityType?> GetByNameAsync(string name, Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all active entity types
    /// </summary>
    Task<IEnumerable<WorkflowEntityType>> GetActiveEntityTypesAsync(Guid tenantId, CancellationToken cancellationToken = default);
}
