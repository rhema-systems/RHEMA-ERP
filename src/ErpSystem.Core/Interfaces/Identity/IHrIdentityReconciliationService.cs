using ErpSystem.Core.DTOs.Identity;
using ErpSystem.Core.Entities.Identity;

namespace ErpSystem.Core.Interfaces.Identity;

public interface IHrIdentityReconciliationService
{
    Task<HrIdentityReconciliationRunDto> RunAsync(
        Guid tenantId,
        Guid? requestedById,
        HrIdentityReconciliationTrigger trigger,
        string? idempotencyKey = null,
        IReadOnlyCollection<Guid>? employeeIds = null,
        CancellationToken cancellationToken = default);

    Task<HrIdentityReconciliationDashboardDto> GetDashboardAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<HrIdentityUserOptionDto>> GetEligibleReplacementUsersAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default);

    Task<HrIdentityWorkflowIssueDto> ResolveWorkflowIssueAsync(
        Guid tenantId,
        Guid issueId,
        Guid replacementUserId,
        Guid actorUserId,
        string resolutionNote,
        CancellationToken cancellationToken = default);

    Task<HrIdentityReconciliationStateDto> ApproveReactivationAsync(
        Guid tenantId,
        Guid userId,
        Guid actorUserId,
        string reviewNote,
        CancellationToken cancellationToken = default);

    Task<HrIdentityReconciliationRunDto> RetryFailedRunAsync(
        Guid tenantId,
        Guid runId,
        Guid actorUserId,
        CancellationToken cancellationToken = default);
}
