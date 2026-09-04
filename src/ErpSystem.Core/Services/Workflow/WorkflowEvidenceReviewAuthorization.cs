using ErpSystem.Core.Entities.Workflow;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Services.Workflow;

/// <summary>
/// Resolves evidence reviewers from this exact workflow's materialized assignments and
/// actual approval actors, without maintaining a separate list of business job titles.
/// </summary>
public static class WorkflowEvidenceReviewAuthorization
{
    // Preserve the existing cross-module evidence-administration capability. These are
    // governance roles, not replacements for configured TDC/business workflow assignments.
    private static readonly HashSet<string> GovernanceRoles = new(StringComparer.OrdinalIgnoreCase)
    {
        "SystemAdmin", "WorkflowAdmin", "SuperAdmin", "TenantAdmin"
    };

    /// <summary>
    /// The caller must first validate active tenant membership and supply current persisted
    /// role memberships. The caller also enforces evidence-specific independence and current
    /// version checks. Review does not complete an approval or bypass its signature/SOD guards.
    /// </summary>
    public static bool CanReview(
        WorkflowInstance instance,
        IReadOnlyCollection<WorkflowApproval> approvals,
        Guid userId,
        IReadOnlyCollection<string> currentRoles)
    {
        if (userId == Guid.Empty || instance.Id == Guid.Empty || instance.TenantId == Guid.Empty ||
            instance.IsDeleted || instance.Status is not (WorkflowInstanceStatus.InProgress or
                WorkflowInstanceStatus.Waiting or WorkflowInstanceStatus.Completed))
        {
            return false;
        }

        var roles = new HashSet<string>(
            currentRoles.Where(role => !string.IsNullOrWhiteSpace(role)).Select(role => role.Trim()),
            StringComparer.OrdinalIgnoreCase);
        if (roles.Overlaps(GovernanceRoles))
        {
            return true;
        }

        return approvals.Any(approval => CanReviewAssignedEvidence(instance, approval, userId, roles));
    }

    private static bool CanReviewAssignedEvidence(
        WorkflowInstance instance,
        WorkflowApproval approval,
        Guid userId,
        HashSet<string> roles)
    {
        var step = approval.StepInstance;
        if (approval.Id == Guid.Empty || approval.IsDeleted || approval.TenantId != instance.TenantId ||
            step == null || step.Id == Guid.Empty || approval.StepInstanceId != step.Id ||
            step.IsDeleted || step.TenantId != instance.TenantId || step.WorkflowInstanceId != instance.Id ||
            step.Status is not (WorkflowStepInstanceStatus.Pending or WorkflowStepInstanceStatus.InProgress or
                WorkflowStepInstanceStatus.Completed))
        {
            return false;
        }

        // A reviewer who actually approved can verify the exact workflow's supporting
        // content after completion only while their assignment authority remains current.
        // A historical decision is not a permanent grant after its role is revoked.
        if (approval.Status == WorkflowApprovalStatus.Approved)
        {
            return approval.ProcessedById == userId && HasCurrentAssignment(approval, userId, roles);
        }

        if (instance.Status == WorkflowInstanceStatus.Completed ||
            approval.Status != WorkflowApprovalStatus.Pending ||
            step.WorkflowStepId == Guid.Empty || step.WorkflowStepId != instance.CurrentStepId ||
            step.Status is not (WorkflowStepInstanceStatus.Pending or WorkflowStepInstanceStatus.InProgress))
        {
            return false;
        }

        // A direct (including delegated) assignment takes precedence over any retained role
        // label. Queued sequential groups never reach this branch.
        return HasCurrentAssignment(approval, userId, roles);
    }

    private static bool HasCurrentAssignment(WorkflowApproval approval, Guid userId, HashSet<string> roles) =>
        approval.ApproverId.HasValue
            ? approval.ApproverId.Value == userId
            : !string.IsNullOrWhiteSpace(approval.ApproverRole) && roles.Contains(approval.ApproverRole.Trim());
}
