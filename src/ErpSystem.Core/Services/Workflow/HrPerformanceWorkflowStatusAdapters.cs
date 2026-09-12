using ErpSystem.Core.Entities.HR.Performance;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;

namespace ErpSystem.Core.Services.Workflow;

/// <summary>
/// Workflow status adapter for <see cref="AppraisalTemplate"/> — the "a unit drafts an
/// appraisal form, HR signs it off" approval.
///
/// This is the one approval in the Performance area that belongs on the generic engine.
/// <see cref="TemplateApprovalStatus"/> is a pure approval lifecycle with a single writer,
/// unlike <c>GoalStatus</c>, which mixes approval with execution state and is deliberately
/// kept on its own command service. Routing here is genuinely configurable — who signs off a
/// senior-management form need not be who signs off a shop-floor one — which is exactly what
/// a workflow definition is for.
///
/// Adapters are auto-discovered by the assembly scan in
/// <c>WorkflowServiceCollectionExtensions</c>; there is no DI registration to add.
///
/// ⚠ <c>SubmittedById</c> / <c>ApprovedById</c> on the entity are bare <c>Guid?</c> columns
/// with no navigation, and the pre-engine controller stamped them from the token's
/// <c>employee_id</c>. The engine hands adapters an <b>ApplicationUser</b> id, so
/// <see cref="AppraisalTemplateService"/> writes the employee id itself and the adapter is
/// left to own only the status and the timestamps.
/// </summary>
public sealed class AppraisalTemplateWorkflowStatusAdapter : IWorkflowStatusAdapter
{
    public IReadOnlyCollection<string> EntityTypes { get; } = new[]
    {
        "AppraisalTemplate",
        "Appraisal Template",
        "APPRAISAL_TEMPLATE"
    };

    public void ApplySubmitOutcome(object entity, WorkflowOutcome outcome, Guid? userId)
        => Apply(Require(entity), outcome);

    public void ApplyApprovalOutcome(object entity, WorkflowOutcome outcome, Guid? userId, string? rejectionReason = null)
    {
        var template = Require(entity);
        Apply(template, outcome);

        if (outcome == WorkflowOutcome.Rejected && !string.IsNullOrWhiteSpace(rejectionReason))
        {
            template.RejectionReason = rejectionReason.Trim();
        }
    }

    public void ApplyRecallOutcome(object entity, Guid? userId, string? reason = null)
        => Apply(Require(entity), WorkflowOutcome.Recalled);

    private static void Apply(AppraisalTemplate template, WorkflowOutcome outcome)
    {
        switch (outcome)
        {
            case WorkflowOutcome.Approved:
                template.ApprovalStatus = TemplateApprovalStatus.Approved;
                template.ApprovalDate = DateTime.UtcNow;
                template.RejectionReason = null;
                break;
            case WorkflowOutcome.Rejected:
                // The decision date is kept — "when was this turned down" is the question
                // asked of a rejected template — but the approver stamp is not.
                template.ApprovalStatus = TemplateApprovalStatus.Rejected;
                template.ApprovalDate = DateTime.UtcNow;
                template.ApprovedById = null;
                break;
            case WorkflowOutcome.Recalled:
                // Back to the author's hands: the submission itself is undone, so the
                // submitted stamp goes with it.
                template.ApprovalStatus = TemplateApprovalStatus.Draft;
                template.SubmittedById = null;
                template.SubmittedDate = null;
                template.ApprovedById = null;
                template.ApprovalDate = null;
                template.RejectionReason = null;
                break;
            default:
                template.ApprovalStatus = TemplateApprovalStatus.PendingApproval;
                template.ApprovedById = null;
                template.ApprovalDate = null;
                template.RejectionReason = null;
                break;
        }
    }

    private static AppraisalTemplate Require(object entity)
        => entity as AppraisalTemplate ?? throw new InvalidOperationException("Expected AppraisalTemplate entity.");
}
