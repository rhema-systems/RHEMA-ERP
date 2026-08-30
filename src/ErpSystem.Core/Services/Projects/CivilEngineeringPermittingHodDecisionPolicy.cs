using ErpSystem.Core.DTOs.Projects;
using ErpSystem.Core.Entities.Projects;

namespace ErpSystem.Core.Services.Projects;

public static class CivilEngineeringPermittingHodDecisionPolicy
{
    public static IReadOnlyList<string> Validate(DecideCivilEngineeringPermittingReviewRequest request)
    {
        var errors = new List<string>();
        if (request.ClientRequestId == Guid.Empty) errors.Add("A client request identifier is required for safe retry.");
        if (!Enum.IsDefined(request.Outcome)) errors.Add("Select a supported HOD permitting decision.");
        var reason = request.Reason?.Trim();
        if ((request.Outcome is CivilEngineeringPermittingHodDecisionOutcome.Reject or CivilEngineeringPermittingHodDecisionOutcome.ReturnForCorrection) && string.IsNullOrWhiteSpace(reason)) errors.Add("Enter a reason when rejecting or returning an engineering recommendation.");
        if (reason?.Length > 2000) errors.Add("The HOD decision reason cannot exceed 2,000 characters.");
        return errors;
    }

    public static string WorkflowAction(CivilEngineeringPermittingHodDecisionOutcome outcome) => outcome == CivilEngineeringPermittingHodDecisionOutcome.Approve ? "Approve" : "Reject";
    public static CivilEngineeringPermittingEngineeringReviewStage Stage(CivilEngineeringPermittingHodDecisionOutcome outcome) => outcome switch
    {
        CivilEngineeringPermittingHodDecisionOutcome.Approve => CivilEngineeringPermittingEngineeringReviewStage.HodApproved,
        CivilEngineeringPermittingHodDecisionOutcome.Reject => CivilEngineeringPermittingEngineeringReviewStage.HodRejected,
        _ => CivilEngineeringPermittingEngineeringReviewStage.HodReturned,
    };
}
