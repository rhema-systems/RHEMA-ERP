using ErpSystem.Core.Entities.Workflow;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Services.Workflow;

public static class WorkflowApprovalSequenceCoordinator
{
    public static WorkflowApprovalStatus GetInitialStatus(
        WorkflowApprovalActivationMode activationMode,
        int approvalGroup,
        int firstGroup)
    {
        return activationMode == WorkflowApprovalActivationMode.Sequential && approvalGroup != firstGroup
            ? WorkflowApprovalStatus.Queued
            : WorkflowApprovalStatus.Pending;
    }

    public static int? GetNextQueuedGroup(IEnumerable<WorkflowApproval> approvals)
    {
        return approvals
            .Where(approval => approval.Status == WorkflowApprovalStatus.Queued)
            .Select(approval => Math.Max(approval.ApprovalGroup, 1))
            .OrderBy(group => group)
            .Cast<int?>()
            .FirstOrDefault();
    }

    public static bool IsGroupSatisfied(
        WorkflowApprovalType approvalType,
        int minApprovalsRequired,
        IReadOnlyCollection<WorkflowApproval> approvals)
    {
        if (approvals.Count == 0)
        {
            return false;
        }

        var approvedCount = approvals.Count(approval => approval.Status == WorkflowApprovalStatus.Approved);
        var effectiveMinimum = Math.Min(Math.Max(minApprovalsRequired, 1), approvals.Count);

        return approvalType switch
        {
            WorkflowApprovalType.Single => approvedCount >= effectiveMinimum,
            WorkflowApprovalType.Multiple => approvedCount == approvals.Count,
            WorkflowApprovalType.Consensus => approvedCount == approvals.Count,
            WorkflowApprovalType.Majority => approvedCount >= Math.Max(
                effectiveMinimum,
                (int)Math.Ceiling(approvals.Count / 2.0)),
            _ => approvedCount >= effectiveMinimum
        };
    }

    public static bool AreAllSequentialGroupsSatisfied(
        WorkflowApprovalType approvalType,
        int minApprovalsRequired,
        IReadOnlyCollection<WorkflowApproval> approvals)
    {
        return approvals.Count > 0 && approvals
            .GroupBy(approval => Math.Max(approval.ApprovalGroup, 1))
            .All(group => IsGroupSatisfied(approvalType, minApprovalsRequired, group.ToList()));
    }
}
