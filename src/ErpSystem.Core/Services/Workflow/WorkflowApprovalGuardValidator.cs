using ErpSystem.Core.DTOs.Workflow;
using ErpSystem.Core.Entities.Workflow;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Services.Workflow;

public static class WorkflowApprovalGuardValidator
{
    public static List<string> Validate(
        WorkflowApprovalConfigDto? config,
        Guid initiatedById,
        IReadOnlyCollection<WorkflowApproval> approvals,
        Guid userId,
        bool enforceSeparation = true)
    {
        var errors = new List<string>();
        if (config == null || userId == Guid.Empty || !enforceSeparation)
        {
            return errors;
        }

        if (config.PreventInitiatorApproval && initiatedById == userId)
        {
            errors.Add("The workflow initiator cannot approve this step.");
        }

        if (config.RequireDistinctApprovers && approvals.Any(approval =>
                approval.Status == WorkflowApprovalStatus.Approved &&
                approval.ProcessedById == userId))
        {
            errors.Add("A different user must complete each approval slot in this step.");
        }

        return errors;
    }
}
