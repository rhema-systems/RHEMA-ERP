using ErpSystem.Core.DTOs.Projects;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Services.Projects;

public static class CivilEngineeringPermittingEngineeringReviewPolicy
{
    public static IReadOnlyList<string> ValidateSubmit(SubmitCivilEngineeringPermittingEngineeringReviewRequest request)
    {
        var errors = new List<string>();
        if (request.ClientRequestId == Guid.Empty) errors.Add("A client request identifier is required for safe retry.");
        if (request.CommentCategoryId == Guid.Empty) errors.Add("Select a configured engineering comment category.");
        if (string.IsNullOrWhiteSpace(request.ReviewComment) || request.ReviewComment.Trim().Length < 5) errors.Add("Enter an engineering review comment of at least 5 characters.");
        if (request.ReviewComment?.Trim().Length > 4000) errors.Add("The engineering review comment cannot exceed 4,000 characters.");
        if (request.CentralDocumentRecordId == Guid.Empty || request.CentralDocumentVersionId == Guid.Empty) errors.Add("Select the current Published central-DMS engineering drawing or review evidence.");
        return errors;
    }

    public static bool NeedsHodDecision(CivilEngineeringPermittingOutcome outcome) => outcome is not CivilEngineeringPermittingOutcome.ReturnForCorrection;
}
