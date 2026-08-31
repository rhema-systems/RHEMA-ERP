namespace ErpSystem.Core.Services.Workflow;

using ErpSystem.Core.Entities.Workflow;

public sealed record WorkflowDelegationResolution(
    Guid DelegationId,
    Guid PrincipalUserId,
    Guid DelegateUserId,
    string Reason,
    bool AllowRedelegation);

public interface IWorkflowRuntimeGovernanceService
{
    /// <summary>
    /// Fails before a workflow instance is created when a mandatory, unconditional approval stage
    /// has no active tenant user who can satisfy its configured actor and segregation-of-duties
    /// rules. Conditional and runtime-derived assignments remain subject to the normal step-entry
    /// checks because their actors cannot be determined safely at workflow start.
    /// </summary>
    Task EnsureMandatoryApprovalActorsAvailableAsync(
        Guid tenantId,
        Guid initiatedById,
        string workflowName,
        IReadOnlyCollection<WorkflowStep> steps,
        CancellationToken cancellationToken = default);

    Task<WorkflowDelegationResolution?> ResolveDelegateAsync(
        Guid tenantId,
        Guid principalUserId,
        string? module,
        string? entityType,
        Guid? workflowDefinitionId,
        Guid? workflowStepId,
        decimal? amount,
        string? currencyCode,
        DateTime effectiveAt,
        CancellationToken cancellationToken = default);

    Task ValidateOneOffDelegationAsync(
        Guid tenantId,
        Guid principalUserId,
        Guid delegateUserId,
        bool allowRedelegation,
        CancellationToken cancellationToken = default);

    Task<DateTime?> CalculateDueDateAsync(
        Guid tenantId,
        DateTime startedAtUtc,
        double? workingHours,
        CancellationToken cancellationToken = default);
}
