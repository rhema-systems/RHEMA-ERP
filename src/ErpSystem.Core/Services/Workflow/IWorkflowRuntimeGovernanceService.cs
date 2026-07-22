namespace ErpSystem.Core.Services.Workflow;

public sealed record WorkflowDelegationResolution(
    Guid DelegationId,
    Guid PrincipalUserId,
    Guid DelegateUserId,
    string Reason,
    bool AllowRedelegation);

public interface IWorkflowRuntimeGovernanceService
{
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
