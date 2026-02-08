using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Interfaces;

/// <summary>
/// Maps workflow outcomes into module-specific status fields
/// </summary>
public interface IWorkflowStatusAdapter
{
    /// <summary>
    /// Entity type names this adapter can handle (aliases allowed)
    /// </summary>
    IReadOnlyCollection<string> EntityTypes { get; }

    /// <summary>
    /// Applies workflow outcome after submission
    /// </summary>
    void ApplySubmitOutcome(object entity, WorkflowOutcome outcome, Guid? userId);

    /// <summary>
    /// Applies workflow outcome after approval processing
    /// </summary>
    void ApplyApprovalOutcome(object entity, WorkflowOutcome outcome, Guid? userId, string? rejectionReason = null);
}

/// <summary>
/// Resolves workflow status adapters by entity type
/// </summary>
public interface IWorkflowStatusAdapterRegistry
{
    bool TryGetAdapter(string entityType, out IWorkflowStatusAdapter adapter);
    IWorkflowStatusAdapter GetAdapter(string entityType);
}
